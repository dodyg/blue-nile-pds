namespace atompds;

public class StaticConfig
{
    public const string DbVersion = "1.0.0";
    public static string Version => $"atompds v{typeof(StaticConfig).Assembly.GetName().Version!.ToString(3)}";
}