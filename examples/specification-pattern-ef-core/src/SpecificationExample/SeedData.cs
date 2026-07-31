using Microsoft.EntityFrameworkCore;
using SpecificationExample.Data;
using SpecificationExample.Domain;

namespace SpecificationExample;

public static class SeedData
{
    public static readonly Guid DemoTenantId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");

    public static readonly Guid OtherTenantId =
        Guid.Parse("10000000-0000-0000-0000-000000000002");

    public static async Task InitializeAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var alice = new Customer
        {
            Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
            Email = "alice@example.com"
        };

        var bob = new Customer
        {
            Id = Guid.Parse("20000000-0000-0000-0000-000000000002"),
            Email = "bob@example.com"
        };

        Order[] orders =
        [
            CreateOrder(
                "30000000-0000-0000-0000-000000000001",
                DemoTenantId,
                alice,
                new DateTime(2026, 7, 29, 10, 0, 0, DateTimeKind.Utc),
                total: 150m,
                paid: 100m,
                OrderStatus.Pending),
            CreateOrder(
                "30000000-0000-0000-0000-000000000002",
                DemoTenantId,
                alice,
                new DateTime(2026, 7, 28, 10, 0, 0, DateTimeKind.Utc),
                total: 220m,
                paid: 220m,
                OrderStatus.Paid),
            CreateOrder(
                "30000000-0000-0000-0000-000000000003",
                DemoTenantId,
                alice,
                new DateTime(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc),
                total: 90m,
                paid: 20m,
                OrderStatus.Processing),
            CreateOrder(
                "30000000-0000-0000-0000-000000000004",
                DemoTenantId,
                bob,
                new DateTime(2026, 7, 30, 10, 0, 0, DateTimeKind.Utc),
                total: 300m,
                paid: 50m,
                OrderStatus.Processing),
            CreateOrder(
                "30000000-0000-0000-0000-000000000005",
                OtherTenantId,
                alice,
                new DateTime(2026, 7, 30, 11, 0, 0, DateTimeKind.Utc),
                total: 500m,
                paid: 0m,
                OrderStatus.Pending)
        ];

        dbContext.Orders.AddRange(orders);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Order CreateOrder(
        string id,
        Guid tenantId,
        Customer customer,
        DateTime createdAt,
        decimal total,
        decimal paid,
        OrderStatus status)
    {
        var orderId = Guid.Parse(id);

        return new Order
        {
            Id = orderId,
            TenantId = tenantId,
            CustomerId = customer.Id,
            Customer = customer,
            CreatedAt = createdAt,
            Total = total,
            Status = status,
            Items =
            [
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductName = "Mechanical Keyboard",
                    Quantity = 1,
                    UnitPrice = total
                }
            ],
            Payments =
            [
                new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    Amount = paid
                }
            ]
        };
    }
}
