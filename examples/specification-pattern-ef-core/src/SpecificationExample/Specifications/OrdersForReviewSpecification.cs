using System.Linq.Expressions;
using SpecificationExample.Domain;

namespace SpecificationExample.Specifications;

public sealed record SearchOrdersRequest(
    Guid TenantId,
    string? CustomerEmail,
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    decimal? MinimumTotal,
    IReadOnlyCollection<OrderStatus>? Statuses,
    bool OnlyWithUnpaidBalance,
    int Page,
    int PageSize);

public static class OrderCriteria
{
    /// <summary>
    /// The business criteria shared by the review page and the export query.
    /// EF Core prunes the null-parameter branches while compiling the query,
    /// so an unused filter never reaches the generated SQL.
    /// </summary>
    public static Expression<Func<Order, bool>> ForReview(SearchOrdersRequest request)
    {
        var email = request.CustomerEmail?.Trim();
        var statuses = request.Statuses;

        return order =>
            order.TenantId == request.TenantId &&
            (email == null ||
                order.Customer.Email.Contains(email)) &&
            (request.CreatedFrom == null ||
                order.CreatedAt >= request.CreatedFrom) &&
            (request.CreatedTo == null ||
                order.CreatedAt < request.CreatedTo) &&
            (request.MinimumTotal == null ||
                order.Total >= request.MinimumTotal) &&
            (statuses == null ||
                statuses.Count == 0 ||
                statuses.Contains(order.Status)) &&
            (!request.OnlyWithUnpaidBalance ||
                order.Payments.Sum(payment => payment.Amount) < order.Total);
    }
}

public sealed class OrdersForReviewSpecification : Specification<Order>
{
    public OrdersForReviewSpecification(SearchOrdersRequest request)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageSize, 1);

        SetCriteria(OrderCriteria.ForReview(request));

        AddInclude(order => order.Customer);
        AddInclude(order => order.Items);

        ApplyOrderByDescending(
            order => order.CreatedAt,
            thenByDescending: order => order.Id);

        ApplyPaging(
            (request.Page - 1) * request.PageSize,
            request.PageSize);

        AsNoTracking();
    }
}
