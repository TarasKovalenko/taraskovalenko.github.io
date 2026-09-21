using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace ExpenseMcp.Security;

public sealed class ClientEntitlementService(IOptions<McpSecurityOptions> options)
{
    private readonly McpSecurityOptions _options = options.Value;

    public AccessDecision Authorize(
        ClaimsPrincipal principal,
        IReadOnlyCollection<string> requiredScopes)
    {
        var clientId = principal.FindFirstValue("client_id") ?? principal.FindFirstValue("azp");
        if (string.IsNullOrWhiteSpace(clientId) ||
            !_options.ClientScopes.TryGetValue(clientId, out var allowedScopes))
        {
            return AccessDecision.Deny("client_not_allowed", requiredScopes);
        }

        var tokenScopes = ScopeSet.From(principal);
        var allowed = allowedScopes.ToHashSet(StringComparer.Ordinal);
        var missing = requiredScopes
            .Where(scope => !tokenScopes.Contains(scope) || !allowed.Contains(scope))
            .ToArray();

        return missing.Length == 0
            ? AccessDecision.Allow()
            : AccessDecision.Deny("insufficient_scope", requiredScopes);
    }
}

public sealed record AccessDecision(
    bool Succeeded,
    string? Error,
    IReadOnlyCollection<string> RequiredScopes)
{
    public static AccessDecision Allow() => new(true, null, []);

    public static AccessDecision Deny(string error, IReadOnlyCollection<string> scopes) =>
        new(false, error, scopes);
}
