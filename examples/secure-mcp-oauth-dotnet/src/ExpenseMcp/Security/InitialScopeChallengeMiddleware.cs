using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace ExpenseMcp.Security;

public sealed class InitialScopeChallengeMiddleware(
    RequestDelegate next,
    IOptions<McpSecurityOptions> options)
{
    private readonly McpSecurityOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized &&
                context.Request.Path.Equals("/mcp", StringComparison.OrdinalIgnoreCase) &&
                context.Response.Headers.TryGetValue(HeaderNames.WWWAuthenticate, out var challenge) &&
                !challenge.ToString().Contains("scope=", StringComparison.Ordinal))
            {
                context.Response.Headers[HeaderNames.WWWAuthenticate] =
                    $"{challenge}, scope=\"{_options.InitialScope}\"";
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}
