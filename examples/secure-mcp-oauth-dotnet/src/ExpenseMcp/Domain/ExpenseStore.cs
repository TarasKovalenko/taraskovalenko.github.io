using System.Collections.Concurrent;

namespace ExpenseMcp.Domain;

public sealed class ExpenseStore
{
    private readonly ConcurrentDictionary<Guid, Expense> _expenses = new(
        new[]
        {
            new KeyValuePair<Guid, Expense>(
                Guid.Parse("7fce98d1-d91e-44b0-aab7-440af78d18af"),
                new Expense(
                    Guid.Parse("7fce98d1-d91e-44b0-aab7-440af78d18af"),
                    "tenant-a",
                    "Train to client workshop",
                    84.50m,
                    "EUR",
                    ExpenseStatus.Pending))
        });

    public IReadOnlyCollection<Expense> List(string tenantId) =>
        _expenses.Values.Where(expense => expense.TenantId == tenantId).ToArray();

    public Expense Approve(Guid id, string tenantId)
    {
        while (true)
        {
            if (!_expenses.TryGetValue(id, out var current) || current.TenantId != tenantId)
            {
                throw new KeyNotFoundException("Expense was not found.");
            }

            var approved = current with { Status = ExpenseStatus.Approved };
            if (_expenses.TryUpdate(id, approved, current))
            {
                return approved;
            }
        }
    }
}

public sealed record Expense(
    Guid Id,
    string TenantId,
    string Description,
    decimal Amount,
    string Currency,
    ExpenseStatus Status);

public enum ExpenseStatus
{
    Pending,
    Approved
}
