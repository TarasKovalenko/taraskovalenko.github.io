namespace RefundAgent.Domain;

public interface IOrderRepository
{
    Task<Order?> FindAuthorizedAsync(
        string tenantId,
        string userId,
        Guid orderId,
        CancellationToken cancellationToken);
}

public interface IRefundGateway
{
    Task<RefundResult> CreateAsync(
        Order order,
        decimal amount,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

