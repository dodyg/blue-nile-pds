namespace Config;

public abstract record InvitesConfig
{
    public abstract bool Required { get; }
}

public record RequiredInvitesConfig : InvitesConfig
{
    public override bool Required => true;
    public int? Interval { get; init; }
    public int Epoch { get; init; }
}

public record NonRequiredInvitesConfig : InvitesConfig
{
    public override bool Required => false;
}