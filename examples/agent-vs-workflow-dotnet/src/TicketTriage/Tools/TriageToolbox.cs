using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.AI;
using TicketTriage.Data;
using TicketTriage.Policy;

namespace TicketTriage.Tools;

/// <summary>
/// Read-only lookups the model may call. Everything that writes or moves money stays out of here:
/// tool access is a permission boundary, not a convenience.
/// </summary>
public sealed class TriageToolbox
{
    private readonly List<string> _calls = [];

    public IReadOnlyList<string> Calls => _calls;

    public int CallCount => _calls.Count;

    [Description("Returns the support plan, monthly spend, and seat count for a customer id.")]
    public string LookupCustomer([Description("Customer id, for example 'acme'.")] string customerId)
    {
        _calls.Add($"lookup_customer({customerId})");

        var customer = DemoData.Customers.FirstOrDefault(c => c.Id == customerId);
        return customer is null
            ? $"No customer with id '{customerId}'."
            : string.Create(
                CultureInfo.InvariantCulture,
                $"id={customer.Id}; name={customer.Name}; plan={customer.Plan}; monthly_spend_usd={customer.MonthlySpendUsd}; seats={customer.SeatCount}");
    }

    [Description("Searches open incidents by product area, for example 'checkout-api' or 'sso'.")]
    public string SearchKnownIssues([Description("Product area to search for.")] string productArea)
    {
        _calls.Add($"search_known_issues({productArea})");

        var matches = DemoData.KnownIssues
            .Where(i => i.ProductArea.Contains(productArea, StringComparison.OrdinalIgnoreCase))
            .Select(i => $"{i.Id}: {i.Summary}")
            .ToList();

        return matches.Count == 0 ? "No open incident matches that product area." : string.Join("\n", matches);
    }

    [Description("Returns the refund rules: who qualifies and what the maximum amount is.")]
    public string GetRefundPolicy()
    {
        _calls.Add("get_refund_policy()");

        return string.Create(
            CultureInfo.InvariantCulture,
            $"A refund needs a reported payment problem and an explicit request from the customer. Free plan does not qualify. Maximum {TriagePolicy.RefundCapUsd} USD, and a human approves every refund.");
    }

    public IList<AITool> AsTools() =>
    [
        CustomerTool(),
        KnownIssuesTool(),
        RefundPolicyTool(),
    ];

    /// <summary>Only what a billing specialist needs.</summary>
    public IList<AITool> BillingTools() => [CustomerTool(), RefundPolicyTool()];

    /// <summary>Only what a platform specialist needs.</summary>
    public IList<AITool> PlatformTools() => [CustomerTool(), KnownIssuesTool()];

    private AIFunction CustomerTool() => AIFunctionFactory.Create(LookupCustomer, "lookup_customer");

    private AIFunction KnownIssuesTool() => AIFunctionFactory.Create(SearchKnownIssues, "search_known_issues");

    private AIFunction RefundPolicyTool() => AIFunctionFactory.Create(GetRefundPolicy, "get_refund_policy");
}
