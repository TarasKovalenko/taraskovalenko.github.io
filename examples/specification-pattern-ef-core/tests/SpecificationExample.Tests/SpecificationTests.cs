using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpecificationExample.Data;
using SpecificationExample.Domain;
using SpecificationExample.Specifications;
using Xunit;

namespace SpecificationExample.Tests;

public sealed class SpecificationTests : IAsyncLifetime
{
    private SqliteConnection connection = null!;
    private AppDbContext dbContext = null!;

    public async ValueTask InitializeAsync()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        dbContext = new AppDbContext(options);
        await SeedData.InitializeAsync(dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await dbContext.DisposeAsync();
        await connection.DisposeAsync();
    }

    private static SearchOrdersRequest Request(
        Guid? tenantId = null,
        string? email = null,
        DateTime? from = null,
        DateTime? to = null,
        decimal? minimumTotal = null,
        IReadOnlyCollection<OrderStatus>? statuses = null,
        bool onlyUnpaid = false,
        int page = 1,
        int pageSize = 10) =>
        new(
            tenantId ?? SeedData.DemoTenantId,
            email,
            from,
            to,
            minimumTotal,
            statuses,
            onlyUnpaid,
            page,
            pageSize);

    [Fact]
    public void Criteria_rejects_order_from_another_tenant()
    {
        var specification = new OrdersForReviewSpecification(Request());
        var predicate = specification.Criteria!.Compile();

        var order = new Order
        {
            TenantId = SeedData.OtherTenantId,
            Customer = new Customer { Email = "alice@example.com" }
        };

        Assert.False(predicate(order));
    }

    [Fact]
    public void Paging_arguments_are_validated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new OrdersForReviewSpecification(Request(page: 0)));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new OrdersForReviewSpecification(Request(pageSize: 0)));
    }

    [Fact]
    public async Task Query_returns_only_orders_of_the_requested_tenant()
    {
        var specification = new OrdersForReviewSpecification(Request());

        var orders = await SpecificationEvaluator
            .GetQuery(dbContext.Orders, specification)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(orders);
        Assert.All(orders, order => Assert.Equal(SeedData.DemoTenantId, order.TenantId));
    }

    [Fact]
    public void Unused_filters_do_not_reach_the_generated_sql()
    {
        var specification = new OrdersForReviewSpecification(Request());

        var sql = SpecificationEvaluator
            .GetQuery(dbContext.Orders, specification)
            .ToQueryString();

        // EF Core prunes the "parameter == null" branches while compiling.
        Assert.DoesNotContain("IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("\"TenantId\"", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Populated_filters_are_translated_to_sql()
    {
        var request = Request(
            email: "alice",
            from: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            to: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            minimumTotal: 100m,
            statuses: [OrderStatus.Pending],
            onlyUnpaid: true);

        var specification = new OrdersForReviewSpecification(request);

        var orders = await SpecificationEvaluator
            .GetQuery(dbContext.Orders, specification)
            .ToListAsync(TestContext.Current.CancellationToken);

        var order = Assert.Single(orders);
        Assert.Equal("alice@example.com", order.Customer.Email);
        Assert.Equal(150m, order.Total);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task Count_ignores_paging()
    {
        var repository = new EfRepository<Order>(dbContext);
        var firstPage = new OrdersForReviewSpecification(Request(pageSize: 1));

        var count = await repository.CountAsync(
            firstPage,
            TestContext.Current.CancellationToken);

        var items = await repository.ListAsync(
            firstPage,
            TestContext.Current.CancellationToken);

        Assert.Equal(4, count);
        Assert.Single(items);
    }

    [Fact]
    public async Task Orders_are_sorted_by_creation_date_descending()
    {
        var repository = new EfRepository<Order>(dbContext);

        var orders = await repository.ListAsync(
            new OrdersForReviewSpecification(Request()),
            TestContext.Current.CancellationToken);

        var expected = orders
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Select(order => order.Id)
            .ToList();

        Assert.Equal(expected, orders.Select(order => order.Id).ToList());
    }
}
