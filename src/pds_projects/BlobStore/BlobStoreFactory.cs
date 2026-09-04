using System;
using BlueNilePds.Pds.Config;
using BlueNilePds.Core.Repo;

namespace BlueNilePds.Pds.BlobStore;

public class BlobStoreFactory(
    BlobStoreConfig config
)
{
    public IBlobStore Create(string did) => config switch 
    {
        DiskBlobstoreConfig diskConfig => new DiskBlobStore(
            did,
            diskConfig.TempLocation ?? "~/.pds_data/temp",
            diskConfig.Location
        ),
        S3BlobstoreConfig s3Config => new S3BlobStore(
            did,
            s3Config.Bucket,
            s3Config.Region,
            s3Config.Endpoint,
            s3Config.ForcePathStyle,
            s3Config.AccessKeyId,
            s3Config.SecretAccessKey,
            TimeSpan.FromMilliseconds(s3Config.UploadTimeoutMs)
        ),
        _ => throw new NotSupportedException("Unsupported blobstore config type")
    };

}
