using Microsoft.EntityFrameworkCore;

namespace SpecificationExample.Specifications;

public static class SpecificationEvaluator
{
    public static IQueryable<T> GetQuery<T>(
        IQueryable<T> inputQuery,
        ISpecification<T> specification,
        bool criteriaOnly = false)
        where T : class
    {
        var query = inputQuery;

        if (specification.Criteria is not null)
        {
            query = query.Where(specification.Criteria);
        }

        // CountAsync needs the criteria only: includes, ordering and paging
        // would change the result or add work the count does not need.
        if (criteriaOnly)
        {
            return query;
        }

        query = specification.Includes.Aggregate(
            query,
            (current, include) => current.Include(include));

        query = ApplyOrdering(query, specification);

        if (specification.Skip is not null)
        {
            query = query.Skip(specification.Skip.Value);
        }

        if (specification.Take is not null)
        {
            query = query.Take(specification.Take.Value);
        }

        if (specification.IsSplitQuery)
        {
            query = query.AsSplitQuery();
        }

        if (specification.IsNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query;
    }

    private static IQueryable<T> ApplyOrdering<T>(
        IQueryable<T> query,
        ISpecification<T> specification)
        where T : class
    {
        if (specification.OrderBy is not null)
        {
            var ordered = query.OrderBy(specification.OrderBy);

            return ApplyThenBy(ordered, specification);
        }

        if (specification.OrderByDescending is not null)
        {
            var ordered = query.OrderByDescending(specification.OrderByDescending);

            return ApplyThenBy(ordered, specification);
        }

        return query;
    }

    private static IQueryable<T> ApplyThenBy<T>(
        IOrderedQueryable<T> ordered,
        ISpecification<T> specification)
        where T : class
    {
        if (specification.ThenBy is not null)
        {
            return ordered.ThenBy(specification.ThenBy);
        }

        if (specification.ThenByDescending is not null)
        {
            return ordered.ThenByDescending(specification.ThenByDescending);
        }

        return ordered;
    }
}
