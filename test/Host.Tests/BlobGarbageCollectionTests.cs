using BlueNilePds.Core.CID;
using BlueNilePds.Host.Services;
using BlueNilePds.Pds.ActorStore.Db;
using BlueNilePds.Pds.BlobStore;
using BlueNilePds.Pds.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlueNilePds.Host.Tests;

public class BlobGarbageCollectionTests
{
    private const string Did = "did:plc:gctest";

    [Test]
    public async Task RunOnceAsync_CollectsOnlyOrphanedBlobsAsync()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "blob-gc-" + Guid.NewGuid().ToString("N"));
        try
        {
            var actorsDir = Path.Combine(tempRoot, "actors");
            var storeDir = Path.Combine(actorsDir, "shard", "did_plc_gctest");
            Directory.CreateDirectory(storeDir);
            var storePath = Path.Combine(storeDir, "store.sqlite");

            var options = new DbContextOptionsBuilder<ActorStoreDb>()
                .UseSqlite($"Data Source={storePath}")
                .Options;
            using (var seed = new ActorStoreDb(options))
            {
                await seed.Database.MigrateAsync();
            }

            var now = DateTime.UtcNow;
            var orphanTempCid = Cid.Create("orphan-temp").ToString();
            var orphanPermCid = Cid.Create("orphan-perm").ToString();
            var referencedCid = Cid.Create("referenced").ToString();
            var freshTempCid = Cid.Create("fresh-temp").ToString();

            using (var seed = new ActorStoreDb(options))
            {
                seed.RepoRoots.Add(new RepoRoot { Did = Did, Cid = Cid.Create("root").ToString(), Rev = "rev-1", IndexedAt = now });
                seed.Blobs.Add(new Blob { Cid = orphanTempCid, MimeType = "image/png", Size = 3, CreatedAt = now.AddDays(-2), Status = BlobStatus.Temporary });
                seed.Blobs.Add(new Blob { Cid = orphanPermCid, MimeType = "image/png", Size = 3, CreatedAt = now.AddDays(-2), Status = BlobStatus.Permanent });
                seed.Blobs.Add(new Blob { Cid = referencedCid, MimeType = "image/png", Size = 3, CreatedAt = now.AddDays(-2), Status = BlobStatus.Permanent });
                seed.Blobs.Add(new Blob { Cid = freshTempCid, MimeType = "image/png", Size = 3, CreatedAt = now, Status = BlobStatus.Temporary });
                seed.RecordBlobs.Add(new RecordBlob { BlobCid = referencedCid, RecordUri = $"at://{Did}/app.bsky.feed.post/abc" });
                await seed.SaveChangesAsync();
            }

            // A second store without a repo root must be skipped, not fail the cycle.
            var emptyStoreDir = Path.Combine(actorsDir, "shard", "did_plc_empty");
            Directory.CreateDirectory(emptyStoreDir);
            var emptyOptions = new DbContextOptionsBuilder<ActorStoreDb>()
                .UseSqlite($"Data Source={Path.Combine(emptyStoreDir, "store.sqlite")}")
                .Options;
            using (var empty = new ActorStoreDb(emptyOptions))
            {
                await empty.Database.MigrateAsync();
            }

            var blobLocation = Path.Combine(tempRoot, "blobs");
            var blobDidDir = Path.Combine(blobLocation, Did);
            Directory.CreateDirectory(blobDidDir);
            foreach (var cid in new[] { orphanTempCid, orphanPermCid, referencedCid, freshTempCid })
            {
                await File.WriteAllTextAsync(Path.Combine(blobDidDir, cid), "blob-bytes");
            }

            var services = new ServiceCollection();
            services.AddSingleton(new ActorStoreConfig { Directory = actorsDir });
            services.AddSingleton(new BlobStoreFactory(new DiskBlobstoreConfig
            {
                Location = blobLocation,
                TempLocation = Path.Combine(tempRoot, "temp")
            }));
            await using var provider = services.BuildServiceProvider();
            var svc = new BlobGarbageCollectionService(provider, NullLogger<BlobGarbageCollectionService>.Instance);

            await svc.RunOnceAsync();

            using (var check = new ActorStoreDb(options))
            {
                var remaining = await check.Blobs.Select(b => b.Cid).ToListAsync();
                await Assert.That(remaining.Contains(orphanTempCid)).IsFalse();
                await Assert.That(remaining.Contains(orphanPermCid)).IsFalse();
                await Assert.That(remaining.Contains(referencedCid)).IsTrue();
                await Assert.That(remaining.Contains(freshTempCid)).IsTrue();
            }

            await Assert.That(File.Exists(Path.Combine(blobDidDir, orphanTempCid))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(blobDidDir, orphanPermCid))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(blobDidDir, referencedCid))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(blobDidDir, freshTempCid))).IsTrue();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}
