using Microsoft.EntityFrameworkCore;
using SpecificationExample;
using SpecificationExample.Data;
using SpecificationExample.Domain;
using SpecificationExample.Specifications;

// SQLite in-memory: a real relational provider, so the generated SQL is the
// SQL a production database would receive. The EF Core InMemory provider
// would accept expressions no relational provider can translate.
var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
await connection.OpenAsync();

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite(connection)
    .Options;

await using var dbContext = new AppDbContext(options);
await SeedData.InitializeAsync(dbContext);

var request = new SearchOrdersRequest(
    TenantId: SeedData.DemoTenantId,
    CustomerEmail: "@example.com",
    CreatedFrom: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
    CreatedTo: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
    MinimumTotal: 100m,
    Statuses: [OrderStatus.Pending, OrderStatus.Processing],
    OnlyWithUnpaidBalance: true,
    Page: 1,
    PageSize: 10);

var repository = new EfRepository<Order>(dbContext);
var service = new SearchOrdersService(repository);

var specification = new OrdersForReviewSpecification(request);
Console.WriteLine("SQL produced by the specification:");
Console.WriteLine(repository.ToQueryString(specification));
Console.WriteLine();

var result = await service.SearchAsync(request);

Console.WriteLine(
    FormattableString.Invariant(
        $"Found {result.TotalCount} orders; page {result.Page}:"));

foreach (var order in result.Items)
{
    var line = FormattableString.Invariant(
        $"- {order.Customer.Email,-20} total={order.Total,6:0.00} status={order.Status}");

    Console.WriteLine(line);
}

// Payments are deliberately not part of the specification: a third Include
// alongside Items would multiply rows. A projection answers the same question
// with one flat result set.
var balances = await dbContext.Orders
    .Where(OrderCriteria.ForReview(request))
    .Select(order => new
    {
        order.Id,
        Paid = order.Payments.Sum(payment => payment.Amount),
        order.Total
    })
    .AsNoTracking()
    .ToListAsync();

Console.WriteLine();
Console.WriteLine("Outstanding balance (projection, no Include):");

foreach (var balance in balances)
{
    Console.WriteLine(
        FormattableString.Invariant(
            $"- {balance.Id} outstanding={balance.Total - balance.Paid,6:0.00}"));
}
