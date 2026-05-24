using System.Threading.Tasks;

namespace Common.Tests;

public class TIDTests
{
    [Test]
    public async Task CreatTidAsync()
    {
        var tid = TID.Next();
        var str = tid.ToString();
        await Assert.That(str.Length).IsEqualTo(13);
    }

    [Test]
    public async Task ParseTidAsync()
    {
        var tid = TID.Next();
        var str = tid.ToString();
        var parsed = TID.FromStr(str);
        await Assert.That(tid.GetTimestamp()).IsEqualTo(parsed.GetTimestamp());
        await Assert.That(tid.GetClockId()).IsEqualTo(parsed.GetClockId());
        await Assert.That(parsed).IsEqualTo(tid);
    }

    [Test]
    public async Task ThrowsOnInvalidTidAsync()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Task.Run(() => TID.FromStr("")));
    }

    [Test]
    public async Task NextStringAsync()
    {
        var prev = TID.FromTime((DateTimeOffset.Now.ToUnixTimeMilliseconds() + 5000) * 1000, 0);
        var prevStr = prev.ToString();
        var nextStr = TID.NextStr(prevStr);
        await Assert.That(string.CompareOrdinal(prevStr, nextStr) < 0).IsTrue();
    }

    [Test]
    public async Task S32EncodingRoundTripsAndPreservesOrderingAsync()
    {
        const long smaller = 32;
        const long larger = 33;

        var smallerEncoded = S32.Encode(smaller);
        var largerEncoded = S32.Encode(larger);

        await Assert.That(S32.Decode(smallerEncoded)).IsEqualTo(smaller);
        await Assert.That(S32.Decode(largerEncoded)).IsEqualTo(larger);
        await Assert.That(string.CompareOrdinal(smallerEncoded, largerEncoded) < 0).IsTrue();
    }


    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(31)]
    public async Task FromTimeRoundTripsTimestampAndClockIdAsync(long clockId)
    {
        var timestamp = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 5000) * 1000;
        var tid = TID.FromTime(timestamp, clockId);

        await Assert.That(tid.GetTimestamp()).IsEqualTo(timestamp);
        await Assert.That(tid.GetClockId()).IsEqualTo(clockId);
    }

    [Test]
    public async Task OrderingAsync()
    {
        var oldest = TID.Next();
        var newest = TID.Next();

        await Assert.That(newest.NewerThan(oldest)).IsTrue();
        await Assert.That(oldest.NewerThan(newest)).IsFalse();
        await Assert.That(oldest.OlderThan(newest)).IsTrue();
        await Assert.That(newest.OlderThan(oldest)).IsFalse();
    }

    [Test]
    public async Task EqualityAsync()
    {
        var tid1 = TID.Next();
        var tid2 = TID.FromStr(tid1.ToString());
        await Assert.That(tid2).IsEqualTo(tid1);
    }

    [Test]
    public async Task InequalityAsync()
    {
        var tid1 = TID.Next();
        var tid2 = TID.Next();
        await Assert.That(tid2).IsNotEqualTo(tid1);
    }
}
