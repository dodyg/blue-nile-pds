using CID;

namespace Repo.Car;

public class CarWriter : IDisposable, IAsyncDisposable
{
    private readonly CarEncoder _encoder;

    private CarWriter(CarEncoder encoder)
    {
        _encoder = encoder;
    }
    public ReadOnlySpan<byte> Bytes => _encoder.Bytes;

    public async ValueTask DisposeAsync()
    {
        await _encoder.DisposeAsync();
    }

    public void Dispose()
    {
        _encoder.Dispose();
    }
    public static async Task<CarWriter> Create(Cid? root)
    {
        var encoder = new CarEncoder();
        if (root != null)
        {
            await encoder.SetRoots(root.Value);
        }

        return new CarWriter(encoder);
    }

    public async Task Put(CarBlock block)
    {
        await _encoder.WriteBlock(block);
    }
}