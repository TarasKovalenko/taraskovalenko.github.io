using System.Collections.Concurrent;
using RefundAgent.Domain;

namespace RefundAgent.Infrastructure;

public sealed class InMemoryRefundGateway : IRefundGateway
{
    private readonly ConcurrentDictionary<string, RefundResult> _refunds = new();

    public int CreatedCount => _refunds.Count;

    public Task<RefundResult> CreateAsync(
        Order order,
        decimal amount,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        bool wasAlreadyProcessed = _refunds.ContainsKey(idempotencyKey);
        RefundResult result = _refunds.GetOrAdd(
            idempotencyKey,
            _ => new(
                Guid.NewGuid(),
                order.Id,
                amount,
                order.Currency,
                WasAlreadyProcessed: false));

        return Task.FromResult(
            result with { WasAlreadyProcessed = wasAlreadyProcessed });
    }
}

