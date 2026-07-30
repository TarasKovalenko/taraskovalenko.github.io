using RefundAgent.Domain;

namespace RefundAgent.Infrastructure;

public static class SampleData
{
    public static readonly Guid OrderId =
        Guid.Parse("3d6d90b8-c11b-43a1-a690-540a962daeb0");

    public static Order Order { get; } = new(
        OrderId,
        TenantId: "tenant-demo",
        UserId: "user-demo",
        RefundableAmount: 250.00m,
        Currency: "USD");
}

