namespace RefundAgent.Domain;

public sealed record AgentExecutionContext(
    string UserId,
    string TenantId,
    IReadOnlySet<string> Scopes,
    string CorrelationId);

