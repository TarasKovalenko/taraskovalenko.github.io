using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace ExpenseMcp.Security;

public sealed class ToolAuthorizationMiddleware(
    RequestDelegate next,
    IOptions<McpSecurityOptions> options,
    ILogger<ToolAuthorizationMiddleware> logger)
{
    private readonly McpSecurityOptions _options = options.Value;

    public async Task InvokeAsync(
        HttpContext context,
        ClientEntitlementService entitlements)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var toolName = await McpRequestInspector.ReadToolNameAsync(
            context.Request,
            context.RequestAborted);

        if (toolName is null)
        {
            await next(context);
            return;
        }

        context.Items[Audit.AuditMiddleware.ToolNameKey] = toolName;
        if (!_options.ToolScopes.TryGetValue(toolName, out var requiredScopes))
        {
            await DenyAsync(
                context,
                toolName,
                AccessDecision.Deny("unconfigured_tool", [_options.InitialScope]));
            return;
        }

        var decision = entitlements.Authorize(context.User, requiredScopes);
        if (decision.Succeeded)
        {
            await next(context);
            return;
        }

        await DenyAsync(context, toolName, decision);
    }

    private Task DenyAsync(HttpContext context, string toolName, AccessDecision decision)
    {
        SecurityLog.ToolAccessDenied(logger, toolName, decision.Error ?? "unknown");

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        var scopes = string.Join(' ', decision.RequiredScopes);
        var metadata = GetAbsoluteMetadataUri(context.Request);
        context.Response.Headers[HeaderNames.WWWAuthenticate] =
            $"Bearer error=\"insufficient_scope\", scope=\"{scopes}\", " +
            $"resource_metadata=\"{metadata}\"";
        return Task.CompletedTask;
    }

    private string GetAbsoluteMetadataUri(HttpRequest request)
    {
        var configured = new Uri(_options.ResourceMetadata, UriKind.RelativeOrAbsolute);
        if (configured.IsAbsoluteUri)
        {
            return configured.AbsoluteUri;
        }

        var separator = configured.OriginalString.StartsWith('/') ? string.Empty : "/";
        return $"{request.Scheme}://{request.Host.ToUriComponent()}{request.PathBase}" +
               $"{separator}{configured.OriginalString}";
    }
}
