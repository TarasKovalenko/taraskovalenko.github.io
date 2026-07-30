using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;

namespace RefundAgent.Domain;

public sealed class RefundTools(
    AgentExecutionContext executionContext,
    IOrderRepository orders,
    IRefundGateway refunds)
{
    [Description("Calculates a refund preview without changing external state.")]
    public async Task<RefundPreview> PreviewRefundAsync(
        Guid orderId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        RequireScope("refunds.read");
        Order order = await GetAuthorizedOrderAsync(orderId, amount, cancellationToken);

        return new(
            order.Id,
            amount,
            order.Currency,
            RequiresApproval: true);
    }

    [Description("Creates a refund. This operation changes payment state and requires approval.")]
    public async Task<RefundResult> ConfirmRefundAsync(
        Guid orderId,
        decimal amount,
        string reason,
        CancellationToken cancellationToken)
    {
        RequireScope("refunds.write");
        Order order = await GetAuthorizedOrderAsync(orderId, amount, cancellationToken);

        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            throw new ArgumentException(
                "A refund reason between 1 and 500 characters is required.",
                nameof(reason));
        }

        string idempotencyKey = CreateIdempotencyKey(
            executionContext.TenantId,
            executionContext.UserId,
            orderId,
            amount,
            reason);

        return await refunds.CreateAsync(
            order,
            amount,
            reason,
            idempotencyKey,
            cancellationToken);
    }

    private async Task<Order> GetAuthorizedOrderAsync(
        Guid orderId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Refund amount must be greater than zero.");
        }

        Order order = await orders.FindAuthorizedAsync(
            executionContext.TenantId,
            executionContext.UserId,
            orderId,
            cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "The order is not available to the current user and tenant.");

        if (amount > order.RefundableAmount)
        {
            throw new InvalidOperationException(
                "Amount exceeds the refundable balance.");
        }

        return order;
    }

    private void RequireScope(string requiredScope)
    {
        if (!executionContext.Scopes.Contains(requiredScope))
        {
            throw new UnauthorizedAccessException(
                $"The '{requiredScope}' scope is required.");
        }
    }

    private static string CreateIdempotencyKey(
        string tenantId,
        string userId,
        Guid orderId,
        decimal amount,
        string reason)
    {
        string canonicalValue = string.Join(
            '\n',
            tenantId,
            userId,
            orderId.ToString("D"),
            amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            reason.Trim());

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalValue));
        return Convert.ToHexString(hash);
    }
}

