namespace ExpenseMcp.Audit;

public sealed partial class LoggerAuditSink(ILogger<LoggerAuditSink> logger) : IAuditSink
{
    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        WriteAuditEvent(logger, auditEvent);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(
        EventId = 4100,
        Level = LogLevel.Information,
        Message = "MCP audit {@AuditEvent}")]
    private static partial void WriteAuditEvent(ILogger logger, AuditEvent auditEvent);
}
