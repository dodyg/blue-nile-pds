namespace BlueNilePds;

public class StaticConfig
{
    public const string DbVersion = "1.0.0";
    public static string Version => $"blue-nile-pds v{typeof(StaticConfig).Assembly.GetName().Version!.ToString(3)}";
}