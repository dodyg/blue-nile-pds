using System.Text;

namespace Common;

public class TID : IComparable<TID>
{
    private static long LastTimestamp = 0;
    private static long TimestampCount = 0;
    private static long? ClockId;
    
    public const int TID_LEN = 13;
    public readonly string Str;
    
    public TID(string str)
    {
        var nodash = DeDash(str);
        if (nodash.Length != TID_LEN)
        {
            throw new ArgumentException($"TID must be {TID_LEN} characters long");
        }
        Str = nodash;
    }
    
    private static string DeDash(string str)
    {
        return str.Replace("-", "");
    }

    public static TID Next(TID? prev = null)
    {
        // append a counter to the timestamp to indicate if multiple timestamps were created within the same millisecond
        // take max of current time & last timestamp to prevent tids moving backwards if system clock drifts backwards
        var time = Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), LastTimestamp);
        if (time == LastTimestamp)
        {
            TimestampCount++;
        }
        LastTimestamp = time;
        var timestamp = time * 1000 + TimestampCount;
        // the bottom 32 clock ids can be randomized & are not guaranteed to be collision resistant
        // we use the same clockid for all tids coming from this machine
        ClockId ??= Random.Shared.Next(0, 32);
        var tid = FromTime(timestamp, ClockId.Value);
        if (prev == null || tid.NewerThan(prev))
        {
            return tid;
        }

        return FromTime(prev.GetTimestamp() + 1, ClockId.Value);
    }

    public static string NextStr(string? prev = null)
    {
        return Next(prev == null ? null : new TID(prev)).Str;
    }
    
    public static TID FromTime(long timestamp, long clockId)
    {
        // pad start with 2s to make sure the string is always 13 characters long
        var str = $"{S32.Encode(timestamp)}{S32.Encode(clockId)}".PadLeft(TID_LEN, '2');
        return new TID(str);
    }
    
    public static TID FromStr(string str)
    {
        return new TID(str);
    }
    
    public static long OldestFirst(TID a, TID b)
    {
        return a.GetTimestamp() - b.GetTimestamp();
    }
    
    public static long NewestFirst(TID a, TID b)
    {
        return b.GetTimestamp() - a.GetTimestamp();
    }

    public long GetTimestamp()
    {
        return S32.Decode(Str[..11]);
    }
    
    public long GetClockId()
    {
        return S32.Decode(Str[11..13]);
    }

    public override string ToString()
    {
        return Str;
    }

    public string Formatted()
    {
        var str = Str;
        return $"{str[..4]}-{str[4..7]}-{str[7..11]}-{str[11..13]}";
    }
    
    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }
        
        return Str == ((TID)obj).Str;
    }
    
    public override int GetHashCode()
    {
        return Str.GetHashCode();
    }
    
    public int CompareTo(TID? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }
        if (other is null)
        {
            return 1;
        }
        return string.Compare(Str, other.Str, StringComparison.Ordinal);
    }
    
    public bool NewerThan(TID other)
    {
        return string.Compare(Str, other.Str, StringComparison.Ordinal) > 0;
    }
    
    public bool OlderThan(TID other)
    {
        return string.Compare(Str, other.Str, StringComparison.Ordinal) < 0;
    }
}

public class S32
{
    private const string S32_CHAR = "234567abcdefghijklmnopqrstuvwxyz";
    public static string Encode(long i)
    {
        var sb = new StringBuilder();
        do
        {
            sb.Append(S32_CHAR[(int)(i % 32)]);
            i /= 32;
        } while (i > 0);
        return sb.ToString();
    }
    
    public static long Decode(string s)
    {
        long i = 0;
        foreach (var c in s)
        {
            i *= 32;
            i += S32_CHAR.IndexOf(c);
        }
        return i;
    }
}