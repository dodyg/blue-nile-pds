using System;
using CID;
using Repo.MST;

namespace Repo.Sync;

public class Provider
{

    public static async Task<(Cid root, BlockMap blocks)> GetRecods(
        IRepoStorage storage,
        Cid commitCid,
        List<(string collection, string rkey)> paths)
    {
        var (obj, _) = await storage.ReadObjAndBytes(commitCid);
        var commit = Commit.FromCborObject(obj);
        var mst = MST.MST.Load(storage, commit.Data);

        var cidForPaths = await Task.WhenAll(
            paths.Select(p => mst.CidsForPath(MST.Util.FormatDataKey(p.collection, p.rkey)))
        );

        var allCids = new CidSet();

        foreach (var cidForPath in cidForPaths)
        {
            allCids.AddSet(new CidSet(cidForPath.ToArray()));
        }

        var found = await storage.GetBlocks(allCids.ToArray());
        if (found.missing.Length > 0)
            throw new Exception("Missing blocks error: " + string.Join(", ", found.missing));

        var map = found.blocks;
        map.Add(commit.ToCborObject());
        return (commitCid, map);
    }
}
