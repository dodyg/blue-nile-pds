using CID;

namespace Repo.Car;

public class CarMemoryWriter : IDisposable, IAsyncDisposable
{
    private readonly MemoryStream _buffer = new();
    public ReadOnlySpan<byte> Bytes => _buffer.ToArray();
    private CarMemoryWriter()
    {
    }
    public async ValueTask DisposeAsync()
    {
        await _buffer.DisposeAsync();
    }

    public void Dispose()
    {
        _buffer.Dispose();
    }
    public static async Task<CarMemoryWriter> Create(Cid? root)
    {
        var writer = new CarMemoryWriter();
        if (root != null)
        {
            var header = CarEncoder.EncodeRoots(root.Value);
            await writer._buffer.WriteAsync(header);
        }
        return writer;
    }

    public async Task Put(CarBlock block)
    {
        var blockBytes = CarEncoder.EncodeBlock(block);
        await _buffer.WriteAsync(blockBytes);
    }
}