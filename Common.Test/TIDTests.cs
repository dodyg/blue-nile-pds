namespace Common.Test;

public class TIDTests
{
    [Fact]
    public void CreatTid()
    {
        var tid = TID.Next();
        var str = tid.ToString();
        Assert.Equal(13, str.Length);
    }
    
    [Fact]
    public void ParseTid()
    {
        var tid = TID.Next();
        var str = tid.ToString();
        var parsed = TID.FromStr(str);
        Assert.Equal(parsed.GetTimestamp(), tid.GetTimestamp());
        Assert.Equal(parsed.GetClockId(), tid.GetClockId());
        Assert.Equal(tid, parsed);
    }
    
    [Fact]
    public void ThrowsOnInvalidTid()
    {
        Assert.Throws<ArgumentException>(() => TID.FromStr(""));
    }
    
    [Fact]
    public void NextString()
    {
        var prev = TID.FromTime((DateTimeOffset.Now.ToUnixTimeMilliseconds() + 5000) * 1000, 0);
        var prevStr = prev.ToString();
        var nextStr = TID.NextStr(prevStr);
        Assert.True(string.CompareOrdinal(prevStr, nextStr) < 0);
    }
    
    [Fact]
    public void Ordering()
    {
        var oldest = TID.Next();
        var newest = TID.Next();

        Assert.True(newest.NewerThan(oldest));
        Assert.False(oldest.NewerThan(newest));
        Assert.True(oldest.OlderThan(newest));
        Assert.False(newest.OlderThan(oldest));
    }
    
    [Fact]
    public void Equality()
    {
        var tid1 = TID.Next();
        var tid2 = TID.FromStr(tid1.ToString());
        Assert.Equal(tid1, tid2);
    }
    
    [Fact]
    public void Inequality()
    {
        var tid1 = TID.Next();
        var tid2 = TID.Next();
        Assert.NotEqual(tid1, tid2);
    }
}