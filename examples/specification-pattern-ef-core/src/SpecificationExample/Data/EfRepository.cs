using Microsoft.EntityFrameworkCore;
using SpecificationExample.Specifications;

namespace SpecificationExample.Data;

public sealed class EfRepository<T>(AppDbContext dbContext)
    where T : class
{
    public Task<List<T>> ListAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        return SpecificationEvaluator
            .GetQuery(dbContext.Set<T>(), specification)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        return SpecificationEvaluator
            .GetQuery(
                dbContext.Set<T>(),
                specification,
                criteriaOnly: true)
            .CountAsync(cancellationToken);
    }

    public string ToQueryString(ISpecification<T> specification)
    {
        return SpecificationEvaluator
            .GetQuery(dbContext.Set<T>(), specification)
            .ToQueryString();
    }
}
