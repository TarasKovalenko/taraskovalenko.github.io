namespace ExpenseMcp.Audit;

public sealed record AuditEvent(
    DateTimeOffset Timestamp,
    string TraceId,
    string Subject,
    string ClientId,
    string Tool,
    string Outcome,
    int StatusCode,
    long DurationMs);
