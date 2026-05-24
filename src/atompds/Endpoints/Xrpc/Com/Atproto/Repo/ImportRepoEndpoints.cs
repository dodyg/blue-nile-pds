using ActorStore;
using ActorStore.Db;
using ActorStore.Repo;
using atompds.Middleware;
using Config;
using Repo;
using Repo.Car;
using Xrpc;
using Cid = CID.Cid;

namespace atompds.Endpoints.Xrpc.Com.Atproto.Repo;

public static class ImportRepoEndpoints
{
    public static RouteGroupBuilder MapImportRepoEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("com.atproto.repo.importRepo", HandleAsync);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        ActorRepositoryProvider actorRepositoryProvider,
        ILogger<Program> logger)
    {
        var did = context.Request.Headers["x-atproto-did"].FirstOrDefault()
                  ?? throw new XRPCError(new InvalidRequestErrorDetail("Missing x-atproto-did header"));

        if (!did.StartsWith("did:"))
            throw new XRPCError(new InvalidRequestErrorDetail("Invalid DID format"));

        if (!actorRepositoryProvider.Exists(did))
            throw new XRPCError(new InvalidRequestErrorDetail("Repo not found for DID"));

        await using var actorStore = actorRepositoryProvider.Open(did);

        using var ms = new MemoryStream();
        await context.Request.Body.CopyToAsync(ms);
        var carBytes = ms.ToArray();

        if (carBytes.Length == 0)
            throw new XRPCError(new InvalidRequestErrorDetail("Empty CAR file"));

        var (header, blocks) = ParseCarFile(carBytes);
        if (header.Roots.Count == 0)
            throw new XRPCError(new InvalidRequestErrorDetail("CAR file has no roots"));

        var rootCid = header.Roots[0];
        logger.LogInformation("Importing repo {Did} with root {RootCid}, {BlockCount} blocks", did, rootCid, blocks.Count);

        await actorStore.TransactRepoAsync(async repo =>
        {
            foreach (var block in blocks)
            {
                var existing = await actorStore.TransactDbAsync<bool?>(async db =>
                    await db.RepoBlocks.FindAsync(block.Cid.ToString()) != null);

                if (existing != true)
                {
                    await actorStore.TransactDbAsync<int>(async db =>
                    {
                        db.RepoBlocks.Add(new RepoBlock
                        {
                            Cid = block.Cid.ToString(),
                            RepoRev = "import",
                            Size = block.Bytes.Length,
                            Content = block.Bytes
                        });
                        await db.SaveChangesAsync();
                        return 0;
                    });
                }
            }
            return true;
        });

        return Results.Ok(new { success = true });
    }

    private static (CarHeader header, List<CarBlock> blocks) ParseCarFile(byte[] carBytes)
    {
        var offset = 0;
        var headerLength = ReadVarInt(carBytes, ref offset);
        var headerBytes = carBytes.AsSpan(offset, headerLength);
        offset += headerLength;

        var headerCbor = PeterO.Cbor.CBORObject.DecodeFromBytes(headerBytes.ToArray());
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
            if (blockLength == 0 || offset + blockLength > carBytes.Length) break;

            var blockData = carBytes.AsSpan(offset, blockLength);
            offset += blockLength;

            var cidLength = ReadCidLength(blockData);
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
            return blockData.Length;
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
