namespace Config;

public abstract record ApprovalConfig
{
    public abstract bool Required { get; }
}

public record RequiredApprovalConfig : ApprovalConfig
{
    public override bool Required => true;
}

public record NonRequiredApprovalConfig : ApprovalConfig
{
    public override bool Required => false;
}
