using SpecificationExample.Data;
using SpecificationExample.Domain;
using SpecificationExample.Specifications;

namespace SpecificationExample;

public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed class SearchOrdersService(EfRepository<Order> orderRepository)
{
    public async Task<PagedResult<Order>> SearchAsync(
        SearchOrdersRequest request,
        CancellationToken cancellationToken = default)
    {
        var specification = new OrdersForReviewSpecification(request);

        var totalCount = await orderRepository.CountAsync(
            specification,
            cancellationToken);

        var orders = await orderRepository.ListAsync(
            specification,
            cancellationToken);

        return new PagedResult<Order>(
            orders,
            totalCount,
            request.Page,
            request.PageSize);
    }
}
