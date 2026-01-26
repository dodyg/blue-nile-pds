using System;

namespace atompds.Utils;

public static class CursorUtils
{
    const string SEPARATOR = "::";
    public static (string primary, string secondary)? Unpack(string? cursorStr, string separator = SEPARATOR)
    {
        if (string.IsNullOrWhiteSpace(cursorStr))
            return null;

        var parts = cursorStr.Split(separator);

        if (parts.Length != 2)
            throw new ArgumentException("Malformed cursor");

        return (parts[0], parts[1]);
    }

    public static string? Pack((string primary, string secondary)? cursor, string separator = SEPARATOR) => cursor switch 
    {
        null => null,
        (var p, var s) => p + separator + s
    };


    public static (T primary, U secondary)? Unpack<T, U>(
        string? cursorStr,
        Func<string, T> primaryParser,
        Func<string, U> secondaryParser,
        string separator = SEPARATOR
    )
    {
        var unpacked = Unpack(cursorStr, separator);
        if (unpacked is null)
            return null;

        return (primaryParser(unpacked.Value.primary), secondaryParser(unpacked.Value.secondary));
    }

    public static string? Pack<T, U>(
        (T primary, U secondary)? cursor,
        Func<T, string> primaryFormatter,
        Func<U, string> secondaryFormatter,
        string separator = SEPARATOR
    ) => cursor switch
    {
        null => null,
        (var p, var s) => primaryFormatter(p) + separator + secondaryFormatter(s)
    };



}
