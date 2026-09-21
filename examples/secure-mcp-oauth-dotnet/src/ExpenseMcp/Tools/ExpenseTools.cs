using System.ComponentModel;
using System.Security.Claims;
using ExpenseMcp.Domain;
using ExpenseMcp.Security;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ExpenseMcp.Tools;

[McpServerToolType]
public sealed class ExpenseTools(
    IHttpContextAccessor httpContextAccessor,
    ClientEntitlementService entitlements,
    ExpenseStore store)
{
    [McpServerTool(Name = "list_expenses", ReadOnly = true),
     Description("List expenses visible to the current tenant.")]
    public IReadOnlyCollection<Expense> ListExpenses()
    {
        var principal = GetPrincipal();
        Demand(principal, "expenses.read");
        return store.List(GetRequiredClaim(principal, "tenant_id"));
    }

    [McpServerTool(Name = "approve_expense", Destructive = true, Idempotent = true),
     Description("Approve a pending expense in the current tenant.")]
    public Expense ApproveExpense(
        [Description("Expense identifier returned by list_expenses.")] Guid expenseId)
    {
        var principal = GetPrincipal();
        Demand(principal, "expenses.approve");
        return store.Approve(expenseId, GetRequiredClaim(principal, "tenant_id"));
    }

    private ClaimsPrincipal GetPrincipal() =>
        httpContextAccessor.HttpContext?.User ??
        throw new McpException("An authenticated HTTP context is required.");

    private void Demand(ClaimsPrincipal principal, params string[] scopes)
    {
        if (!entitlements.Authorize(principal, scopes).Succeeded)
        {
            throw new McpException("The client is not authorized to call this tool.");
        }
    }

    private static string GetRequiredClaim(ClaimsPrincipal principal, string claimType) =>
        principal.FindFirstValue(claimType) ??
        throw new McpException($"Required claim '{claimType}' is missing.");
}
