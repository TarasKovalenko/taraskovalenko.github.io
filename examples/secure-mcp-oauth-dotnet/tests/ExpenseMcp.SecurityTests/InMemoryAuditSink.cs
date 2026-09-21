using System.Collections.Concurrent;
using ExpenseMcp.Audit;

namespace ExpenseMcp.SecurityTests;

public sealed class InMemoryAuditSink : IAuditSink
{
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    public IReadOnlyCollection<AuditEvent> Events => _events.ToArray();

    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        _events.Enqueue(auditEvent);
        return ValueTask.CompletedTask;
    }
}
