using System.Diagnostics;
using System.Security.Claims;
using ExpenseMcp.Security;

namespace ExpenseMcp.Audit;

public sealed class AuditMiddleware(RequestDelegate next)
{
    public const string ToolNameKey = "mcp.audit.tool";

    public async Task InvokeAsync(HttpContext context, IAuditSink auditSink)
    {
        if (!HttpMethods.IsPost(context.Request.Method) ||
            !context.Request.Path.Equals("/mcp", StringComparison.OrdinalIgnoreCase))
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

        context.Items[ToolNameKey] = toolName;
        var started = Stopwatch.GetTimestamp();
        Exception? failure = null;

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            var principal = context.User;
            var subject = principal.FindFirstValue("sub") ?? "anonymous";
            var clientId = principal.FindFirstValue("client_id") ??
                           principal.FindFirstValue("azp") ??
                           "unknown";
            var statusCode = failure is null
                ? context.Response.StatusCode
                : StatusCodes.Status500InternalServerError;

            var auditEvent = new AuditEvent(
                DateTimeOffset.UtcNow,
                context.TraceIdentifier,
                subject,
                clientId,
                toolName,
                statusCode < 400 ? "allowed" : "denied",
                statusCode,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            await auditSink.WriteAsync(auditEvent, CancellationToken.None);
        }
    }
}
