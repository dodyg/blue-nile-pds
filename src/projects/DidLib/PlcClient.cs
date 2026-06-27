using System.Text;
using System.Text.Json;
using Common;
using PeterO.Cbor;

namespace DidLib;

public record PlcClientConfig(string Host);

public class PlcClient
{
    private readonly HttpClient _client;
    private readonly PlcClientConfig _config;

    public PlcClient(HttpClient client, PlcClientConfig config)
    {
        _client = client;
        _config = config;
    }

    public async Task<string?> GetLastOperationCidAsync(string did)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{_config.Host}/{did}/log");
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.EnumerateArray().ToArray();
        if (arr.Length == 0) return null;
        var lastOpRaw = arr[^1].GetRawText();
        var cbor = CBORObject.FromJSONString(lastOpRaw);
        var block = CborBlock.Encode(cbor);
        return block.Cid.ToString();
    }

    public async Task SendOperationAsync(string did, SignedOp<AtProtoOp> op)
    {
        using var postReq = new HttpRequestMessage(HttpMethod.Post, $"{_config.Host}/{did}");
        postReq.Content = new StringContent(op.ToCborObject().ToJSONString(), Encoding.UTF8, "application/json");
        var resp = await _client.SendAsync(postReq);
        var respStr = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to send operation: {respStr}");
        }
    }

    public async Task SendTombstoneAsync(string did, SignedOp<Tombstone> op)
    {
        using var postReq = new HttpRequestMessage(HttpMethod.Post, $"{_config.Host}/{did}");
        postReq.Content = new StringContent(op.ToCborObject().ToJSONString(), Encoding.UTF8, "application/json");
        var resp = await _client.SendAsync(postReq);
        var respStr = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to send operation: {respStr}");
        }
    }
}