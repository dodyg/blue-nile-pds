using System.Text;

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

    public async Task SendOperation(string did, SignedOp<AtProtoOp> op)
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

    public async Task SendTombstone(string did, SignedOp<Tombstone> op)
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