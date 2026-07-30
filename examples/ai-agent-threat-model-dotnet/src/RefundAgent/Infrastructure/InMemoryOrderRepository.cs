using RefundAgent.Domain;

namespace RefundAgent.Infrastructure;

public sealed class InMemoryOrderRepository(IEnumerable<Order> orders)
    : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _orders =
        orders.ToDictionary(order => order.Id);

    public Task<Order?> FindAuthorizedAsync(
        string tenantId,
        string userId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Order? result = _orders.TryGetValue(orderId, out Order? order)
                        && order.TenantId == tenantId
                        && order.UserId == userId
            ? order
            : null;

        return Task.FromResult(result);
    }
}
