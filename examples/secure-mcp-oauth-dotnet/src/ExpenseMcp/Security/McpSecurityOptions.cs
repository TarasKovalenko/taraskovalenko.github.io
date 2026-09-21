namespace ExpenseMcp.Security;

public sealed class McpSecurityOptions
{
    public const string SectionName = "McpSecurity";

    public required string Resource { get; init; }

    public required string ResourceMetadata { get; init; }

    public required string AuthorizationServer { get; init; }

    public required string InitialScope { get; init; }

    public required Dictionary<string, string[]> ToolScopes { get; init; }

    public required Dictionary<string, string[]> ClientScopes { get; init; }

    public required RateLimitSettings RateLimit { get; init; }
}

public sealed class RateLimitSettings
{
    public int PermitLimit { get; init; } = 60;

    public int WindowSeconds { get; init; } = 60;
}
