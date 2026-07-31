using System.Linq.Expressions;

namespace SpecificationExample.Specifications;

public abstract class Specification<T> : ISpecification<T>
    where T : class
{
    private readonly List<Expression<Func<T, object>>> _includes = [];

    public Expression<Func<T, bool>>? Criteria { get; private set; }

    public IReadOnlyCollection<Expression<Func<T, object>>> Includes => _includes;

    public Expression<Func<T, object>>? OrderBy { get; private set; }

    public Expression<Func<T, object>>? OrderByDescending { get; private set; }

    public Expression<Func<T, object>>? ThenBy { get; private set; }

    public Expression<Func<T, object>>? ThenByDescending { get; private set; }

    public int? Skip { get; private set; }

    public int? Take { get; private set; }

    public bool IsNoTracking { get; private set; }

    public bool IsSplitQuery { get; private set; }

    /// <summary>
    /// Replaces the criteria. Call once per specification, or combine
    /// several predicates with <see cref="Expression.AndAlso"/> first.
    /// </summary>
    protected void SetCriteria(Expression<Func<T, bool>> criteria)
    {
        Criteria = criteria;
    }

    protected void AddInclude(Expression<Func<T, object>> include)
    {
        _includes.Add(include);
    }

    protected void ApplyOrderBy(
        Expression<Func<T, object>> orderBy,
        Expression<Func<T, object>>? thenBy = null)
    {
        OrderBy = orderBy;
        ThenBy = thenBy;
    }

    protected void ApplyOrderByDescending(
        Expression<Func<T, object>> orderByDescending,
        Expression<Func<T, object>>? thenByDescending = null)
    {
        OrderByDescending = orderByDescending;
        ThenByDescending = thenByDescending;
    }

    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
    }

    protected void AsNoTracking()
    {
        IsNoTracking = true;
    }

    protected void AsSplitQuery()
    {
        IsSplitQuery = true;
    }
}
