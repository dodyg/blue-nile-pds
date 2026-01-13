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
        Did = did;
        TempLocation = tempLocation;
        Location = location;
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

    private string GetTempPath(Cid cid) =>
        Path.Join(TempLocation, Did, cid.ToString());


    public async Task PutTemp(Cid cid, byte[] bytes)
    {
        EnsureTemp();
        var path = GetTempPath(cid);
        await File.WriteAllBytesAsync(path, bytes);
    }

    public async Task PutTemp(Cid cid, Stream stream)
    {
        EnsureTemp();
        var path = GetTempPath(cid);
        using var fileStream = File.Create(path);
        await stream.CopyToAsync(fileStream);
    }

    public async Task PutPermanent(Cid cid, byte[] bytes)
    {
        EnsureDirectory();
        var path = GetStoredPath(cid);
        await File.WriteAllBytesAsync(path, bytes);
    }

    public async Task PutPermanent(Cid cid, Stream stream)
    {
        EnsureDirectory();
        var path = GetStoredPath(cid);
        using var fileStream = File.Create(path);
        await stream.CopyToAsync(fileStream);
    }


    public async Task MakePermanent(Cid cid)
    {
        EnsureTemp();
        var tempPath = GetTempPath(cid);

        EnsureDirectory();
        var storedPath = GetStoredPath(cid);

        if (!File.Exists(tempPath) && !File.Exists(storedPath))
        {
            throw new Exception("Blob not found in temp or permanent storage");
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

    public async Task<byte[]> GetBytes(Cid cid)
    {
        var path = GetStoredPath(cid);
        if (!File.Exists(path))
        {
            throw new Exception("Blob not found in permanent storage");
        }

        return await File.ReadAllBytesAsync(path);
    }

    public async Task<Stream> GetStream(Cid cid)
    {
        var path = GetStoredPath(cid);
        if (!File.Exists(path))
        {
            throw new Exception("Blob not found in permanent storage");
        }

        return File.OpenRead(path);
    }

    public async Task Delete(Cid cid)
    {
        var path = GetStoredPath(cid);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public async Task DeleteMany(Cid[] cids)
    {
        // might need to optimize
        var exceptions = new List<Exception>();

        foreach (var cid in cids)
        {
            try
            {
                await Delete(cid);
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
