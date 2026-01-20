using System;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Transfer;
using CID;
using FishyFlip.Lexicon;
using Repo;

namespace BlobStore;

public class S3BlobStore : IBlobStore
{
    readonly string bucket;
    readonly TimeSpan uploadTimeout;
    readonly AmazonS3Client client;
    readonly string did;

    public S3BlobStore(
        string did,
        string bucket,
        string? region,
        string? endpoint,
        bool forcePathStyle,
        string accessKeyId,
        string secretAccessKey,
        TimeSpan uploadTimeout
    )
    {
        this.bucket = bucket;
        this.uploadTimeout = uploadTimeout;
        this.did = did;

        var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);

        var config = new AmazonS3Config
        {
            ForcePathStyle = forcePathStyle,
            Timeout = uploadTimeout,
            // TODO: this should be removed later, only for local testing with garage
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED
        };

        // RegionEndpoint and ServiceURL are mutually exclusive properties. 
        // Whichever property is set last will cause the other to automatically be reset to null.
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            config.ServiceURL = endpoint;
        }
        if (!string.IsNullOrWhiteSpace(region))
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
        }

        client = new AmazonS3Client(credentials, config);
    }
    private string GenKey() => Path.GetRandomFileName();

    private string GetTempPath(string key) =>
        $"tmp/{did}/{key}";

    private string GetStoredPath(Cid cid) =>
        $"blocks/{did}/{cid}";
    

    private async Task PutObjectAsync(string key, Stream stream, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(uploadTimeout);

        var transferUtility = new TransferUtility(client);
        var putRequest = new TransferUtilityUploadRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = stream,

            // !Dangerous: should be used only in trusted environments. remove later
            DisablePayloadSigning = true
        };

        await transferUtility.UploadAsync(putRequest, cts.Token);
    }

    public Task<string> PutTemp(byte[] bytes) =>
        PutTemp(bytes, CancellationToken.None);
    public async Task<string> PutTemp(byte[] bytes, CancellationToken ct)
    {
        var key = GenKey();
        await PutObjectAsync(GetTempPath(key), new MemoryStream(bytes), ct);
        return key;
    }

    public Task<string> PutTemp(Stream stream) =>
        PutTemp(stream, CancellationToken.None);
    public async Task<string> PutTemp(Stream stream, CancellationToken ct)
    {
        var key = GenKey();
        await PutObjectAsync(GetTempPath(key), stream, ct);
        return key;
    }

    public async Task<long> GetTempSize(string key)
    {
        try
        {
            var response = await client.GetObjectMetadataAsync(bucket, GetTempPath(key));
            return response.ContentLength;
        }
        catch (Amazon.S3.AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
        {
            throw new BlobNotFoundException(innerException: ex);
        }
    }


    public async Task PutPermanent(Cid cid, byte[] bytes) =>
        await PutPermanent(cid, bytes, CancellationToken.None);

    public async Task PutPermanent(Cid cid, byte[] bytes, CancellationToken ct)
    {
        await PutObjectAsync(GetStoredPath(cid), new MemoryStream(bytes), ct);
    }

    public async Task PutPermanent(Cid cid, Stream stream) =>
        await PutPermanent(cid, stream, CancellationToken.None);

    public async Task PutPermanent(Cid cid, Stream stream, CancellationToken ct)
    {
        await PutObjectAsync(GetStoredPath(cid), stream, ct);
    }

    async Task Move(string sourceKey, string destKey)
    {
        try
        {
            var copyRequest = new Amazon.S3.Model.CopyObjectRequest
            {
                SourceBucket = bucket,
                SourceKey = sourceKey,
                DestinationBucket = bucket,
                DestinationKey = destKey
            };
            await client.CopyObjectAsync(copyRequest);
        }
        catch (Amazon.S3.AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
        {
            // Already deleted, possibly by a concurrently running process
            throw new BlobNotFoundException(innerException: ex);
        }

        try
        {
            var deleteRequest = new Amazon.S3.Model.DeleteObjectRequest
            {
                BucketName = bucket,
                Key = sourceKey
            };
            await client.DeleteObjectAsync(deleteRequest);
        }
        catch (Amazon.S3.AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
        {
            // Already deleted, possibly by a concurrently running process
            return;
        }
    }

    public async Task MakePermanent(string key, Cid cid)
    {
        try
        {
            // We normally call this method when we know the file is temporary.
            // Because of this, we optimistically move the file, allowing to make
            // fewer network requests in the happy path.
            await Move(GetTempPath(key), GetStoredPath(cid));
        }
        catch (BlobNotFoundException)
        {
            // If the optimistic move failed because the temp file was not found,
            // check if the permanent file already exists. If it does, we can assume
            // that another process made the file permanent concurrently, and we can
            // no-op.
            var alreadyHas = await HasStored(cid);
            if (alreadyHas) return;

            throw;
        }
    }

    public async Task<bool> HasStored(Cid cid)
    {
        return await HasKey(GetStoredPath(cid));
    }

    private async Task<bool> HasKey(string key)
    {
        try
        {
            var response = await client.GetObjectMetadataAsync(bucket, key);
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (Amazon.S3.AmazonS3Exception)
        {
            return false;
        }
    }

    private async Task<Amazon.S3.Model.GetObjectResponse> GetObject(string key)
    {
        var response = await client.GetObjectAsync(bucket, key);
        if (response.ResponseStream == null)
        {
            throw new BlobNotFoundException();
        }
        return response;
    }

    public async Task<byte[]> GetBytes(Cid cid)
    {
        var response = await GetObject(GetStoredPath(cid));
        using var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms);
        return ms.ToArray();
    }

    public async Task<Stream> GetStream(Cid cid)
    {
        var response = await GetObject(GetStoredPath(cid));
        return response.ResponseStream;
    }

    public async Task<Stream> GetTempStream(string key)
    {
        var response = await GetObject(GetTempPath(key));
        return response.ResponseStream;
    }

    public async Task Delete(Cid cid)
    {
        await DeleteKey(GetStoredPath(cid));
    }

    public async Task DeleteMany(Cid[] cids)
    {
        var errors = new List<Exception>();
        
        // S3 DeleteObjects supports up to 1000 keys per request
        foreach (var chunk in cids.Chunk(1000))
        {
            try
            {
                var keys = chunk.Select(cid => GetStoredPath(cid)).ToList();
                await DeleteManyKeys(keys);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException(errors);
        }
    }

    private async Task DeleteKey(string key)
    {
        await client.DeleteObjectAsync(bucket, key);
    }

    private async Task DeleteManyKeys(List<string> keys)
    {
        var deleteRequest = new Amazon.S3.Model.DeleteObjectsRequest
        {
            BucketName = bucket,
            Objects = keys.Select(k => new Amazon.S3.Model.KeyVersion { Key = k }).ToList()
        };
        await client.DeleteObjectsAsync(deleteRequest);
    }

}

public class BlobNotFoundException : Exception
{
    public BlobNotFoundException()
        : base("Blob not found")
    {
    }

    public BlobNotFoundException(string? message = null, Exception? innerException = null)
        : base(message ?? "Blob not found", innerException)
    {
    }
}
