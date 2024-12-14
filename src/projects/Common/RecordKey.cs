using System.Text.RegularExpressions;

namespace Common;

public static partial class RecordKey
{
    public static bool IsValidRecordKey(string rkey)
    {
        try
        {
            EnsureValidRecordKey(rkey);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public static void EnsureValidRecordKey(string rkey)
    {
        if (rkey.Length is > 512 or < 1)
        {
            throw new ArgumentException("Record key must be between 1 and 512 characters");
        }
        
        if (!ValidRKeyRegex().IsMatch(rkey))
        {
            throw new ArgumentException("Record key must only contain alphanumeric characters, underscores, and hyphens");
        }
        
        if (rkey is "." or "..")
        {
            throw new ArgumentException("Record key cannot be '.' or '..'");
        }
    }

    [GeneratedRegex("^[a-zA-Z0-9_~.:-]{1,512}$")]
    private static partial Regex ValidRKeyRegex();
}