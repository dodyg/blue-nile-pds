using CID;

namespace Repo.Car;

public class CarWriter : IDisposable, IAsyncDisposable
{
    private readonly CarEncoder _encoder;
    public ReadOnlySpan<byte> Bytes => _encoder.Bytes;
    public static async Task<CarWriter> Create(Cid? root)
    {
        var encoder = new CarEncoder();
        if (root != null)
        {
            await encoder.SetRoots(root.Value);
        }
        
        return new CarWriter(encoder);
    }
    
    private CarWriter(CarEncoder encoder)
    {
        _encoder = encoder;
    }
        
    public async Task Put(CarBlock block)
    {
        await _encoder.WriteBlock(block);
    }
    
    public void Dispose()
    {
        _encoder.Dispose();
    }
    
    public async ValueTask DisposeAsync()
    {
        await _encoder.DisposeAsync();
    }
}
