using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CID;
using Common;

namespace Repo.MST;

public static partial class Util
{
    public static string FormatDataKey(string collection, string rkey)
    {
        return $"{collection}/{rkey}";
    }
    public static (string collection, string rkey) ParseDataKey(string key)
    {
        var split = key.Split('/');
        if (split.Length != 2)
        {
            throw new Exception($"Invalid record key: {key}");
        }
        return (split[0], split[1]);
    }

    public static Cid CidForEntries(INodeEntry[] entry)
    {
        var data = SerializeNodeData(entry);
        return CborBlock.Encode(data).Cid;
    }

    public static int LeadingZerosOnHash(ReadOnlySpan<byte> key)
    {
        var hash = SHA256.HashData(key);
        var leadingZeros = 0;
        foreach (var item in hash)
        {
            if (item < 64)
            {
                leadingZeros++;
            }
            if (item < 16)
            {
                leadingZeros++;
            }
            if (item < 4)
            {
                leadingZeros++;
            }
            if (item == 0)
            {
                leadingZeros++;
            }
            else
            {
                break;
            }
        }

        return leadingZeros;
    }

    public static int? LayerForEntries(INodeEntry[] entries)
    {
        var firstLeaf = entries.FirstOrDefault(x => x is Leaf);
        if (firstLeaf is not Leaf leaf)
        {
            return null;
        }

        return LeadingZerosOnHash(Encoding.ASCII.GetBytes(leaf.Key));
    }

    public static INodeEntry[] DeserializeNodeData(IRepoStorage storage, NodeData data, MstOpts? opts)
    {
        var entries = new List<INodeEntry>();
        if (data.Left != null)
        {
            entries.Add(MST.Load(storage, data.Left.Value, opts?.Layer == null ? null : new MstOpts(opts.Layer - 1)));
        }
        var lastKey = "";
        foreach (var entry in data.Entries)
        {
            var keyStr = entry.KeyString;
            var key = lastKey[..entry.PrefixCount] + keyStr;
            EnsureValidMstKey(key);
            entries.Add(new Leaf(key, entry.Value));
            lastKey = key;
            if (entry.Tree != null)
            {
                entries.Add(MST.Load(storage, entry.Tree.Value, opts?.Layer == null ? null : new MstOpts(opts.Layer - 1)));
            }
        }

        return entries.ToArray();
    }

    public static NodeData SerializeNodeData(INodeEntry[] entries)
    {
        var data = new NodeData
        {
            Left = null,
            Entries = []
        };

        var i = 0;
        if (entries.Length > 0 && entries[0] is MST mst)
        {
            i++;
            data.Left = mst.Pointer;
        }

        var lastKey = "";
        while (i < entries.Length)
        {
            var entry = entries[i];
            var next = entries.Length > i + 1 ? entries[i + 1] : null;
            if (entry is not Leaf leaf)
            {
                throw new Exception("Not a valid node: two subtrees next to each other");
            }
            i++;
            Cid? subtree = null;
            if (next is MST mst2)
            {
                subtree = mst2.Pointer;
                i++;
            }
            EnsureValidMstKey(leaf.Key);
            var prefixLen = CountPrefixLen(lastKey, leaf.Key);
            data.Entries.Add(new TreeEntry
            {
                PrefixCount = prefixLen,
                Key = Encoding.ASCII.GetBytes(leaf.Key[prefixLen..]),
                Value = leaf.Value,
                Tree = subtree
            });

            lastKey = leaf.Key;
        }

        return data;
    }

    public static int CountPrefixLen(string a, string b)
    {
        var i = 0;
        while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
        return i;
    }

    public static bool EnsureValidMstKey(string str)
    {
        if (!IsValidMstKey(str))
        {
            throw new Exception($"Not a valid MST key: {str}");
        }
        return true;
    }

    public static bool IsValidMstKey(string str)
    {
        var validCharsRegex = ValidCharsRegex();

        var split = str.Split('/');
        if (str.Length > 256)
        {
            return false;
        }
        if (split.Length != 2)
        {
            return false;
        }
        if (split[0].Length == 0)
        {
            return false;
        }
        if (split[1].Length == 0)
        {
            return false;
        }
        if (!validCharsRegex.IsMatch(split[0]))
        {
            return false;
        }
        if (!validCharsRegex.IsMatch(split[1]))
        {
            return false;
        }
        return true;
    }

    public static bool IsValidChars(string str)
    {
        return ValidCharsRegex().IsMatch(str);
    }

    [GeneratedRegex("^[a-zA-Z0-9_\\-:.]*$")]
    private static partial Regex ValidCharsRegex();
}