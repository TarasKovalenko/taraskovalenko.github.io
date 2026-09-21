using ExpenseMcp.Audit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExpenseMcp.SecurityTests;

public sealed class ExpenseMcpFactory(int permitLimit = 100) : WebApplicationFactory<Program>
{
    public const string Issuer = "https://issuer.tests.example";
    public const string Audience = "https://expenses.example.com/mcp";
    public const string SigningKey = "tests-only-signing-key-that-is-long-enough-for-hs256-2026";

    public InMemoryAuditSink AuditSink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Authentication:Issuer", Issuer);
        builder.UseSetting("Authentication:Audience", Audience);
        builder.UseSetting("Authentication:TestSigningKey", SigningKey);
        builder.UseSetting("McpSecurity:Resource", Audience);
        builder.UseSetting("McpSecurity:ResourceMetadata", "/.well-known/oauth-protected-resource");
        builder.UseSetting("McpSecurity:AuthorizationServer", Issuer);
        builder.UseSetting("McpSecurity:InitialScope", "expenses.read");
        builder.UseSetting("McpSecurity:ToolScopes:list_expenses:0", "expenses.read");
        builder.UseSetting("McpSecurity:ToolScopes:approve_expense:0", "expenses.approve");
        builder.UseSetting("McpSecurity:ClientScopes:finance-desktop:0", "expenses.read");
        builder.UseSetting("McpSecurity:ClientScopes:finance-admin:0", "expenses.read");
        builder.UseSetting("McpSecurity:ClientScopes:finance-admin:1", "expenses.approve");
        builder.UseSetting(
            "McpSecurity:RateLimit:PermitLimit",
            permitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("McpSecurity:RateLimit:WindowSeconds", "60");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuditSink>();
            services.AddSingleton<IAuditSink>(AuditSink);
        });
    }
}
