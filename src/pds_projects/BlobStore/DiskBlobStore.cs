using System.Net.Http.Headers;
using CID;
using Repo;

namespace BlobStore;

public class DiskBlobStore : IBlobStore
{
    public string Did { get; }
    public string TempLocation { get; }
    public string Location { get; }
    public DiskBlobStore(
        string did,
        string tempLocation,
        string location
    )
    {
        if (did.Contains("..") || did.Contains('/') || did.Contains('\\'))
            throw new ArgumentException("Invalid DID: potential path traversal");
        Did = did;
        TempLocation = ExpandPath(tempLocation);
        Location = ExpandPath(location);
    }

    private static string ExpandPath(string path)
    {
        if (path.StartsWith("~/"))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        
        if (path == "~")
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
        return path;
    }

    private void EnsureDirectory()
    {
        var path = Path.Join(Location, Did);
        Directory.CreateDirectory(path);
    }

    private void EnsureTemp()
    {
        var path = Path.Join(TempLocation, Did);
        Directory.CreateDirectory(path);
    }

    private string GetStoredPath(Cid cid) =>
        Path.Join(Location, Did, cid.ToString());

    private string GetTempPath(string key) =>
        Path.Join(TempLocation, Did, key);

    private string GenKey() => Path.GetRandomFileName();


    public Task<string> PutTempAsync(byte[] bytes) =>
        PutTempAsync(bytes, CancellationToken.None);

    public async Task<string> PutTempAsync(byte[] bytes, CancellationToken ct)
    {
        EnsureTemp();
        var key = GenKey();
        var path = GetTempPath(key);
        await File.WriteAllBytesAsync(path, bytes);

        return key;
    }

    public Task<string> PutTempAsync(Stream stream) =>
        PutTempAsync(stream, CancellationToken.None);

    public async Task<string> PutTempAsync(Stream stream, CancellationToken ct)
    {
        EnsureTemp();
        var key = GenKey();
        var path = GetTempPath(key);

        using var fileStream = File.Create(path);

        await stream.CopyToAsync(fileStream, ct);
        return key;
    }

    public async Task<long> GetTempSizeAsync(string key)
    {
        var path = GetTempPath(key);
        if (!File.Exists(path))
        {
            throw new Exception("Temp blob not found");
        }
        var info = new FileInfo(path);
        return info.Length;
    }


    public async Task PutPermanentAsync(Cid cid, byte[] bytes) =>
        await PutPermanentAsync(cid, bytes, CancellationToken.None);

    public async Task PutPermanentAsync(Cid cid, byte[] bytes, CancellationToken ct)
    {
        EnsureDirectory();
        var path = GetStoredPath(cid);
        await File.WriteAllBytesAsync(path, bytes, ct);
    }

    public async Task PutPermanentAsync(Cid cid, Stream stream) =>
        await PutPermanentAsync(cid, stream, CancellationToken.None);

    public async Task PutPermanentAsync(Cid cid, Stream stream, CancellationToken ct)
    {
        EnsureDirectory();
        var path = GetStoredPath(cid);
        using var fileStream = File.Create(path);
        await stream.CopyToAsync(fileStream, ct);
    }


    public async Task MakePermanentAsync(string tmpKey, Cid cid)
    {
        EnsureTemp();
        var tempPath = GetTempPath(tmpKey);

        EnsureDirectory();
        var storedPath = GetStoredPath(cid);

        if (!File.Exists(tempPath) && !File.Exists(storedPath))
        {
            throw new Exception($"Blob not found in temp: {tempPath} or already in permanent storage: {storedPath}");
        }


        if (File.Exists(storedPath))
        {
            // file already permanent, delete temp if exists
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            
            return;
        }


        File.Move(tempPath, storedPath);
        await Task.CompletedTask;
    }

    public async Task<byte[]> GetBytesAsync(Cid cid)
    {
        var path = GetStoredPath(cid);
        if (!File.Exists(path))
        {
            throw new Exception("Blob not found in permanent storage");
        }

        return await File.ReadAllBytesAsync(path);
    }

    public async Task<Stream> GetStreamAsync(Cid cid)
    {
        var path = GetStoredPath(cid);
        if (!File.Exists(path))
        {
            throw new Exception("Blob not found in permanent storage");
        }

        return File.OpenRead(path);
    }

    public async Task<Stream> GetTempStreamAsync(string key)
    {
        var path = GetTempPath(key);
        if (!File.Exists(path))
        {
            throw new Exception("Blob not found in temp storage");
        }

        return File.OpenRead(path);
    }

    public async Task DeleteAsync(Cid cid)
    {
        var path = GetStoredPath(cid);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public async Task DeleteManyAsync(Cid[] cids)
    {
        // might need to optimize
        var exceptions = new List<Exception>();

        foreach (var cid in cids)
        {
            try
            {
                await DeleteAsync(cid);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count == 1)
        {
            throw exceptions[0];
        }
        else if (exceptions.Count > 1)
        {
            throw new AggregateException("Multiple errors occurred while deleting blobs", exceptions);
        }
    }

    
}
