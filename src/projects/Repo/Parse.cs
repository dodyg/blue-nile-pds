using CID;
using PeterO.Cbor;

namespace Repo;

public class Parse
{
    public static T GetAndParseByDef<T>(BlockMap blocks, Cid cid, Func<CBORObject, T?> parse)
    {
        var bytes = blocks.Get(cid);
        if (bytes == null)
        {
            throw new MissingBlockException(cid, nameof(GetAndParseByDef));
        }

        return ParseObjByDef(bytes, cid, parse);
    }

    public static T ParseObjByDef<T>(byte[] bytes, Cid cid, Func<CBORObject, T?> parse)
    {
        var obj = CBORObject.DecodeFromBytes(bytes);
        var res = parse(obj);
        if (res == null)
        {
            throw new UnexpectedObjectException(cid, typeof(T).Name, "null");
        }

        return res;
    }
}