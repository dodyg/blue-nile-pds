using Ipfs;
using PeterO.Cbor;
using Cid = CID.Cid;

namespace Repo.Car;

public class CarEncoder : IDisposable, IAsyncDisposable
{
    private readonly MemoryStream _buffer = new();
    public ReadOnlySpan<byte> Bytes => _buffer.ToArray();
    
    public async Task SetRoots(Cid root)
    {
        var bytes = CreateHeader(root);
        await _buffer.WriteAsync(bytes);
    }

    // Note: spec supports multiple roots
    private byte[] CreateHeader(Cid root)
    {
        var headerObj = CBORObject.NewMap();
        headerObj.Add("roots", new[] { root.ToCBORObject() });
        headerObj.Add("version", 1);
        var headerBytes = headerObj.EncodeToBytes()!;
        var varIntBytes = Varint.Encode(headerBytes.Length);
        return [..varIntBytes, ..headerBytes];
    }
            
    public async Task WriteBlock(CarBlock block)
    {
        var cidBytes = block.Cid.ToBytes();
        var varIntBytes = Varint.Encode(cidBytes.Length + block.Bytes.Length);
        byte[] buffer = [..varIntBytes, ..cidBytes, ..block.Bytes];
        await _buffer.WriteAsync(buffer);
    }
    
    public void Dispose()
    {
        _buffer.Dispose();
    }
    
    public async ValueTask DisposeAsync()
    {
        await _buffer.DisposeAsync();
    }
}