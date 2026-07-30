namespace RefundAgent.Domain;

public sealed record Order(
    Guid Id,
    string TenantId,
    string UserId,
    decimal RefundableAmount,
    string Currency);

public sealed record RefundPreview(
    Guid OrderId,
    decimal Amount,
    string Currency,
    bool RequiresApproval);

public sealed record RefundResult(
    Guid RefundId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    bool WasAlreadyProcessed);

