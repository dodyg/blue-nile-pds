using System;
using CID;
using Repo.MST;

namespace Repo.Sync;

public class Provider
{

    public static async Task<(Cid root, BlockMap blocks)> GetRecodsAsync(
        IRepoStorage storage,
        Cid commitCid,
        List<(string collection, string rkey)> paths)
    {
        var (obj, _) = await storage.ReadObjAndBytesAsync(commitCid);
        var commit = Commit.FromCborObject(obj);
        var mst = MST.MST.Load(storage, commit.Data);

        var cidForPaths = await Task.WhenAll(
            paths.Select(p => mst.CidsForPathAsync(MST.Util.FormatDataKey(p.collection, p.rkey)))
        );

        var allCids = new CidSet();

        foreach (var cidForPath in cidForPaths)
        {
            allCids.AddSet(new CidSet(cidForPath.ToArray()));
        }

        var found = await storage.GetBlocksAsync(allCids.ToArray());
        if (found.missing.Length > 0)
            throw new Exception("Missing blocks error: " + string.Join(", ", found.missing));

        var map = found.blocks;
        map.Add(commit.ToCborObject());
        return (commitCid, map);
    }
}
