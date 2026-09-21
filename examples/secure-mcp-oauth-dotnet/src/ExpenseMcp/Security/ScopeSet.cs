using System.Security.Claims;

namespace ExpenseMcp.Security;

public static class ScopeSet
{
    public static IReadOnlySet<string> From(ClaimsPrincipal principal)
    {
        return principal.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Concat(principal.FindAll("scp").SelectMany(
                claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .ToHashSet(StringComparer.Ordinal);
    }
}
