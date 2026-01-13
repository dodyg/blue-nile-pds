using System;
using Config;
using Repo;

namespace BlobStore;

public class BlobStoreFactory(
    DiskBlobstoreConfig config
)
{
    public IBlobStore Create(string did)
    {
        // For now, only DiskBlobStore is supported
        return new DiskBlobStore(
            did,
            config.TempLocation ?? Path.Join(config.Location, "temp"),
            config.Location
        );
    }
}
