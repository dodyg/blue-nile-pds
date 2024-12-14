using System.Buffers.Text;
using System.Security.Cryptography;
using Crypto;
using PeterO.Cbor;
using SimpleBase;

namespace DidLib;

public static class Operations
{
    public static Task<string> GetSignature(CBORObject obj, IKeyPair keyPair)
    {
        var memBuf = obj.EncodeToBytes();
        var sig = keyPair.Sign(memBuf);
        var b64Url = Base64Url.EncodeToString(sig);
        return Task.FromResult(b64Url);
    }

    public static async Task<SignedOp<AtProtoOp>> AtProtoOp(string signingKey, string handle, string pds, string[] rotationKeys, string? cid, IKeyPair keyPair)
    {
        var op = FormatAtProtoOp(signingKey, handle, pds, rotationKeys, cid);
        var sig = await GetSignature(op.ToCborObject(), keyPair);

        return new SignedOp<AtProtoOp>
        {
            Sig = sig,
            Op = op
        };
    }

    public static async Task<(string Did, SignedOp<AtProtoOp> Op)> CreateOp(string signingKey, string handle, string pds, string[] rotationKeys, IKeyPair keyPair)
    {
        var op = await AtProtoOp(signingKey, handle, pds, rotationKeys, null, keyPair);
        var did = await DidForCreateOp(op);
        return (did, op);
    }

    public static Task<string> DidForCreateOp(SignedOp<AtProtoOp> op)
    {
        var memBuf = op.ToCborObject().EncodeToBytes();
        var hashOfGenesis = SHA256.HashData(memBuf);
        var hashB32 = Base32.Rfc4648.Encode(hashOfGenesis);
        var truncated = hashB32[..24].ToLower();
        return Task.FromResult($"did:plc:{truncated}");
    }

    public static AtProtoOp FormatAtProtoOp(string signingKey, string handle, string pds, string[] rotationKeys, string? cid)
    {
        return new AtProtoOp
        {
            Type = "plc_operation",
            VerificationMethods = new Dictionary<string, string>
            {
                {"atproto", signingKey}
            },
            RotationKeys = rotationKeys,
            AlsoKnownAs = [EnsureAtProtoPrefix(handle)],
            Services = new Dictionary<string, Service>
            {
                {"atproto_pds", new Service {Type = "AtprotoPersonalDataServer", Endpoint = EnsureHttpPrefix(pds)}}
            },
            Prev = cid
        };
    }

    private static string EnsureAtProtoPrefix(string str)
    {
        if (str.StartsWith("at://"))
        {
            return str;
        }
        var stripped = str.Replace("http://", "").Replace("https://", "");
        return $"at://{stripped}";
    }

    private static string EnsureHttpPrefix(string str)
    {
        if (str.StartsWith("http://") || str.StartsWith("https://"))
        {
            return str;
        }
        return $"https://{str}";
    }
}