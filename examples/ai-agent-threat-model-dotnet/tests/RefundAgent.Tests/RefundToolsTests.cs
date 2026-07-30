using RefundAgent.Domain;
using RefundAgent.Infrastructure;
using Xunit;

namespace RefundAgent.Tests;

public sealed class RefundToolsTests
{
    [Fact]
    public async Task PreviewReturnsAuthorizedOrderWithoutSideEffect()
    {
        (RefundTools tools, InMemoryRefundGateway gateway) =
            CreateTools("tenant-demo", "user-demo", "refunds.read");

        RefundPreview preview = await tools.PreviewRefundAsync(
            SampleData.OrderId,
            25m,
            CancellationToken.None);

        Assert.Equal(25m, preview.Amount);
        Assert.True(preview.RequiresApproval);
        Assert.Equal(0, gateway.CreatedCount);
    }

    [Fact]
    public async Task PreviewRejectsCrossTenantAccess()
    {
        (RefundTools tools, _) =
            CreateTools("tenant-attacker", "user-demo", "refunds.read");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tools.PreviewRefundAsync(
                SampleData.OrderId,
                25m,
                CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmRequiresWriteScope()
    {
        (RefundTools tools, _) =
            CreateTools("tenant-demo", "user-demo", "refunds.read");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tools.ConfirmRefundAsync(
                SampleData.OrderId,
                25m,
                "Customer request",
                CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmRejectsAmountAboveRefundableBalance()
    {
        (RefundTools tools, _) =
            CreateTools("tenant-demo", "user-demo", "refunds.write");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => tools.ConfirmRefundAsync(
                SampleData.OrderId,
                251m,
                "Customer request",
                CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmIsIdempotentForTheSameNormalizedOperation()
    {
        (RefundTools tools, InMemoryRefundGateway gateway) =
            CreateTools("tenant-demo", "user-demo", "refunds.write");

        RefundResult first = await tools.ConfirmRefundAsync(
            SampleData.OrderId,
            25m,
            "Customer request",
            CancellationToken.None);

        RefundResult second = await tools.ConfirmRefundAsync(
            SampleData.OrderId,
            25m,
            "Customer request",
            CancellationToken.None);

        Assert.Equal(first.RefundId, second.RefundId);
        Assert.False(first.WasAlreadyProcessed);
        Assert.True(second.WasAlreadyProcessed);
        Assert.Equal(1, gateway.CreatedCount);
    }

    private static (RefundTools Tools, InMemoryRefundGateway Gateway) CreateTools(
        string tenantId,
        string userId,
        params string[] scopes)
    {
        var context = new AgentExecutionContext(
            userId,
            tenantId,
            new HashSet<string>(scopes, StringComparer.Ordinal),
            Guid.NewGuid().ToString("N"));

        var repository =
            new InMemoryOrderRepository([SampleData.Order]);
        var gateway = new InMemoryRefundGateway();

        return (
            new RefundTools(context, repository, gateway),
            gateway);
    }
}
