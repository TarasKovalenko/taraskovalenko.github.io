namespace ExpenseMcp.Security;

public static partial class SecurityLog
{
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Warning,
        Message = "Access token validation failed")]
    public static partial void TokenValidationFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Warning,
        Message = "Tool call denied. Tool={Tool}, Reason={Reason}")]
    public static partial void ToolAccessDenied(ILogger logger, string tool, string reason);
}
