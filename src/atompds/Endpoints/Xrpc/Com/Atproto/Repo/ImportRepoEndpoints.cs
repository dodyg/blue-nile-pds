using System.Security.Cryptography;
using ActorStore;
using ActorStore.Db;
using ActorStore.Repo;
using atompds.Config;
using atompds.Middleware;
using CarpaNet;
using CID;
using Config;
using Microsoft.EntityFrameworkCore;
using Multiformats.Codec;
using Multiformats.Hash;
using PeterO.Cbor;
using Repo;
using Repo.Car;
using Repo.MST;
using Xrpc;
using Cid = CID.Cid;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Repo;

public static class ImportRepoEndpoints
{
    public static RouteGroupBuilder MapImportRepoEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.repo.importRepo", HandleAsync)
            .RequireRateLimiting("repo-import")
            .WithMetadata(new AccessStandardAttribute());
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        ActorRepositoryProvider actorRepositoryProvider,
        ServerEnvironment env,
        ILogger<Program> logger)
    {
        if (!env.PDS_ACCEPTING_REPO_IMPORTS)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Repo imports are not accepted"));
        }

        if (context.Request.ContentLength > env.PDS_MAX_REPO_IMPORT_SIZE)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Repo import exceeds maximum size"));
        }

        var did = context.Request.Headers["x-atproto-did"].FirstOrDefault()
                  ?? throw new XRPCError(new InvalidRequestErrorDetail("Missing x-atproto-did header"));

        if (!did.StartsWith("did:"))
            throw new XRPCError(new InvalidRequestErrorDetail("Invalid DID format"));

        if (!actorRepositoryProvider.Exists(did))
            throw new XRPCError(new InvalidRequestErrorDetail("Repo not found for DID"));

        using var ms = new MemoryStream();
        await context.Request.Body.CopyToAsync(ms);
        var carBytes = ms.ToArray();

        if (carBytes.Length == 0)
            throw new XRPCError(new InvalidRequestErrorDetail("Empty CAR file"));

        if (carBytes.Length > env.PDS_MAX_REPO_IMPORT_SIZE)
        {
            throw new XRPCError(new InvalidRequestErrorDetail("Repo import exceeds maximum size"));
        }

        var (header, blocks) = ParseCarFile(carBytes);

        // 1. Validate roots: exactly 1 root
        if (header.Roots.Count != 1)
            throw new XRPCError(new InvalidRequestErrorDetail($"CAR file must have exactly 1 root, got {header.Roots.Count}"));

        var rootCid = header.Roots[0];

        // 2. Verify each block's CID matches its content
        var blockMap = new BlockMap();
        foreach (var block in blocks)
        {
            var hash = Multihash.Encode(SHA256.HashData(block.Bytes), HashType.SHA2_256);
            var expectedCid = Cid.NewV1((ulong)MulticodecCode.MerkleDAGCBOR, hash);
            if (!expectedCid.Equals(block.Cid))
            {
                throw new XRPCError(new InvalidRequestErrorDetail($"Block CID mismatch: expected {expectedCid}, got {block.Cid}"));
            }
            blockMap.Set(block.Cid, block.Bytes);
        }

        // 3. Verify root commit CID is present
        if (!blockMap.Has(rootCid))
            throw new XRPCError(new InvalidRequestErrorDetail("Root commit block not found in CAR"));

        // Parse commit
        Commit commit;
        try
        {
            var commitObj = CBORObject.DecodeFromBytes(blockMap.Get(rootCid)!);
            commit = Commit.FromCborObject(commitObj);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse commit block");
            throw new XRPCError(new InvalidRequestErrorDetail("Invalid commit block"));
        }

        // 4. Load MST and verify structural validity
        var memStorage = new MemoryBlockStore(blockMap);
        var mst = MST.Load(memStorage, commit.Data);
        try
        {
            var allNodes = await mst.AllNodesAsync();
            // Verify all referenced CIDs are present
            foreach (var node in allNodes)
            {
                if (node is MST tree)
                {
                    if (!blockMap.Has(tree.Pointer))
                    {
                        throw new XRPCError(new InvalidRequestErrorDetail($"Missing MST block: {tree.Pointer}"));
                    }
                }
                else if (node is Leaf leaf)
                {
                    if (!blockMap.Has(leaf.Value))
                    {
                        throw new XRPCError(new InvalidRequestErrorDetail($"Missing leaf block: {leaf.Value}"));
                    }
                }
            }
        }
        catch (MissingBlockException ex)
        {
            throw new XRPCError(new InvalidRequestErrorDetail($"Missing block in MST: {ex.Cid}"));
        }
        catch (RepoException ex)
        {
            throw new XRPCError(new InvalidRequestErrorDetail($"Invalid repo structure: {ex.Message}"));
        }

        // 5. Forward-only delta check
        await using var actorStore = actorRepositoryProvider.Open(did);
        var currentRoot = await actorStore.TransactDbAsync(async db =>
        {
            var root = await db.RepoRoots.FirstOrDefaultAsync();
            return root?.Cid;
        });

        if (currentRoot != null)
        {
            var currentCid = Cid.FromString(currentRoot);
            if (commit.Prev == null || !commit.Prev.Value.Equals(currentCid))
            {
                throw new XRPCError(new InvalidRequestErrorDetail("Import is not a valid forward-only delta from current repo"));
            }
        }

        // Store blocks
        await actorStore.TransactRepoAsync(async repo =>
        {
            foreach (var block in blocks)
            {
                var existing = await actorStore.TransactDbAsync<bool>(async db =>
                    await db.RepoBlocks.AnyAsync(x => x.Cid == block.Cid.ToString()));

                if (!existing)
                {
                    await actorStore.TransactDbAsync<int>(async db =>
                    {
                        db.RepoBlocks.Add(new RepoBlock
                        {
                            Cid = block.Cid.ToString(),
                            RepoRev = commit.Rev,
                            Size = block.Bytes.Length,
                            Content = block.Bytes
                        });
                        await db.SaveChangesAsync();
                        return 0;
                    });
                }
            }

            // Update root to imported commit
            await repo.Repo.Storage.UpdateRootAsync(rootCid, commit.Rev);
            return true;
        });

        logger.LogInformation("Imported repo {Did} with root {RootCid}, {BlockCount} blocks", did, rootCid, blocks.Count);
        return Results.Ok(new { success = true });
    }

    private static (CarHeader header, List<CarBlock> blocks) ParseCarFile(byte[] carBytes)
    {
        var offset = 0;
        var headerLength = ReadVarInt(carBytes, ref offset);
        if (headerLength < 0 || offset + headerLength > carBytes.Length)
            throw new XRPCError(new InvalidRequestErrorDetail("Invalid CAR header length"));

        var headerBytes = carBytes.AsSpan(offset, headerLength);
        offset += headerLength;

        var headerCbor = CBORObject.DecodeFromBytes(headerBytes.ToArray());
        var roots = new List<Cid>();
        if (headerCbor.ContainsKey("roots"))
        {
            var rootsArr = headerCbor["roots"];
            for (var i = 0; i < rootsArr.Count; i++)
            {
                var rootCidBytes = rootsArr[i].GetByteString();
                roots.Add(Cid.ReadBytes(rootCidBytes));
            }
        }

        var header = new CarHeader(roots);
        var blocks = new List<CarBlock>();

        while (offset < carBytes.Length)
        {
            var blockLength = ReadVarInt(carBytes, ref offset);
            if (blockLength == 0) break;
            if (offset + blockLength > carBytes.Length)
                throw new XRPCError(new InvalidRequestErrorDetail("Invalid CAR block length"));

            var blockData = carBytes.AsSpan(offset, blockLength);
            offset += blockLength;

            var cidLength = ReadCidLength(blockData);
            if (cidLength > blockData.Length)
                throw new XRPCError(new InvalidRequestErrorDetail("Invalid CID length in CAR block"));

            var cidBytes = blockData.Slice(0, cidLength).ToArray();
            var content = blockData.Slice(cidLength).ToArray();

            var cid = Cid.ReadBytes(cidBytes);
            blocks.Add(new CarBlock(cid, content));
        }

        return (header, blocks);
    }

    private static int ReadVarInt(byte[] data, ref int offset)
    {
        int value = 0, shift = 0;
        while (offset < data.Length)
        {
            var b = data[offset++];
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift > 35)
                throw new XRPCError(new InvalidRequestErrorDetail("Invalid varint in CAR file"));
        }
        return value;
    }

    private static int ReadCidLength(ReadOnlySpan<byte> blockData)
    {
        if (blockData.Length < 2) return blockData.Length;
        var version = blockData[0];
        if (version == 0x01)
        {
            var codec = blockData[1];
            if (codec == 0x55)
            {
                if (blockData.Length < 4) return blockData.Length;
                var hashLen = blockData[3];
                return 4 + hashLen;
            }
            if (blockData.Length < 3) return blockData.Length;
            var hashLenV1 = blockData[2];
            return 3 + hashLenV1;
        }
        if (version == 0x12 || version == 0x55)
        {
            if (blockData.Length < 3) return blockData.Length;
            var hashLen = blockData[2];
            return 3 + hashLen;
        }
        return blockData.Length;
    }
}

public record CarHeader(List<Cid> Roots);
