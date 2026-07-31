using System.Linq.Expressions;

namespace SpecificationExample.Specifications;

public interface ISpecification<T>
    where T : class
{
    Expression<Func<T, bool>>? Criteria { get; }

    IReadOnlyCollection<Expression<Func<T, object>>> Includes { get; }

    Expression<Func<T, object>>? OrderBy { get; }

    Expression<Func<T, object>>? OrderByDescending { get; }

    Expression<Func<T, object>>? ThenBy { get; }

    Expression<Func<T, object>>? ThenByDescending { get; }

    int? Skip { get; }

    int? Take { get; }

    bool IsNoTracking { get; }

    bool IsSplitQuery { get; }
}
