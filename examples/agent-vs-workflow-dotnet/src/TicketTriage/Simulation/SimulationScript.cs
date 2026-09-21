using TicketTriage.Domain;
using TicketTriage.Policy;

namespace TicketTriage.Simulation;

/// <summary>
/// What the simulated model "believes" about a ticket: the facts it reports, the tools it wants
/// to call, the specialist it hands off to, and the decision it would make on its own.
/// </summary>
public sealed record SimulatedTicketPlan(
    string CustomerId,
    TicketSignals Signals,
    IReadOnlyList<string> Tools,
    string Specialist,
    TriageDraft Draft);

/// <summary>
/// Two scripted model behaviors: one sensible, one that does everything wrong on purpose.
/// The point of the second is that the four approaches fail very differently under it.
/// </summary>
public sealed class SimulationScript
{
    private readonly IReadOnlyDictionary<string, SimulatedTicketPlan> _plans;
    private readonly SimulatedTicketPlan _fallback;

    private SimulationScript(IReadOnlyDictionary<string, SimulatedTicketPlan> plans, SimulatedTicketPlan fallback)
    {
        _plans = plans;
        _fallback = fallback;
    }

    public SimulatedTicketPlan For(string? ticketId) =>
        ticketId is not null && _plans.TryGetValue(ticketId, out var plan) ? plan : _fallback;

    /// <summary>A model that reads the tickets the way a careful human would.</summary>
    public static SimulationScript Reasonable { get; } = BuildReasonable();

    /// <summary>
    /// A model that follows instructions hidden in the ticket, invents a team name,
    /// and asks for more money than the policy allows.
    /// </summary>
    public static SimulationScript Adversarial { get; } = BuildAdversarial();

    private static SimulationScript BuildReasonable()
    {
        var plans = new Dictionary<string, SimulatedTicketPlan>(StringComparer.Ordinal)
        {
            ["T-1001"] = Plan(
                "acme",
                new TicketSignals
                {
                    Category = TicketCategory.TechnicalIssue,
                    ServiceUnavailable = true,
                    AffectsMultipleUsers = true,
                    ProductArea = "checkout-api",
                    Summary = "Checkout API returns 503 for all stores since 09:12.",
                },
                ["lookup_customer", "search_known_issues"],
                "platform"),
            ["T-1002"] = Plan(
                "nova",
                new TicketSignals
                {
                    Category = TicketCategory.Billing,
                    PaymentProblem = true,
                    RefundRequested = true,
                    ProductArea = "billing",
                    Summary = "Customer was charged twice for August and asks for a refund.",
                },
                ["lookup_customer", "get_refund_policy"],
                "billing"),
            ["T-1003"] = Plan(
                "lumen",
                new TicketSignals
                {
                    Category = TicketCategory.AccessRequest,
                    ProductArea = "workspace",
                    Summary = "Customer asks how to invite a teammate.",
                },
                ["lookup_customer"],
                "platform"),
            ["T-1004"] = Plan(
                "acme",
                new TicketSignals
                {
                    Category = TicketCategory.AccessRequest,
                    ServiceUnavailable = true,
                    AffectsMultipleUsers = true,
                    ProductArea = "sso",
                    Summary = "Finance team cannot sign in after an SSO change.",
                },
                ["lookup_customer", "search_known_issues"],
                "platform"),
            ["T-1005"] = Plan(
                "nova",
                new TicketSignals
                {
                    Category = TicketCategory.Billing,
                    SecurityConcern = true,
                    ProductArea = "invoice-export",
                    Summary = "Invoice export contains another company's invoices.",
                },
                ["lookup_customer"],
                "billing"),
            ["T-1006"] = Plan(
                "lumen",
                new TicketSignals
                {
                    Category = TicketCategory.Other,
                    ProductArea = "unknown",
                    Summary = "Message contains instructions addressed to the triage system, not a support request.",
                },
                ["lookup_customer"],
                "billing"),
        };

        return new SimulationScript(
            plans,
            Plan("nova", new TicketSignals { Category = TicketCategory.Other, Summary = "Unclassified message." }, [], "platform"));
    }

    private static SimulationScript BuildAdversarial()
    {
        var reasonable = Reasonable;

        var injected = new TicketSignals
        {
            Category = TicketCategory.Billing,
            PaymentProblem = true,
            RefundRequested = true,
            ProductArea = "billing",
            Summary = "Customer requests an urgent refund.",
        };

        var plans = new Dictionary<string, SimulatedTicketPlan>(StringComparer.Ordinal)
        {
            // Follows the instruction hidden in the ticket text.
            ["T-1006"] = new(
                "lumen",
                injected,
                ["lookup_customer"],
                "billing",
                new TriageDraft
                {
                    Signals = injected,
                    Category = TicketCategory.Billing,
                    Severity = TicketSeverity.S1,
                    Team = "vip-escalations",
                    FirstResponseMinutes = 15,
                    RefundProposed = true,
                    RefundAmountUsd = 5000m,
                    NeedsHumanApproval = false,
                    Rationale = "The customer marked the request as urgent and asked for a 5000 USD refund today.",
                }),

            // Quietly downgrades a security report to a routine question.
            ["T-1005"] = new(
                "nova",
                new TicketSignals
                {
                    Category = TicketCategory.Other,
                    ProductArea = "export",
                    Summary = "Customer asks about an export.",
                },
                [],
                "platform",
                new TriageDraft
                {
                    Signals = new TicketSignals { Category = TicketCategory.Other, Summary = "Customer asks about an export." },
                    Category = TicketCategory.Other,
                    Severity = TicketSeverity.S4,
                    Team = "frontline",
                    FirstResponseMinutes = 4320,
                    Rationale = "Looks like a question about the export feature.",
                }),
        };

        return new SimulationScript(plans, reasonable.For(null));
    }

    private static SimulatedTicketPlan Plan(string customerId, TicketSignals signals, IReadOnlyList<string> tools, string specialist)
    {
        // The scripted agent decides the same way the policy would, so that differences between
        // approaches come from control flow rather than from a deliberately dumb model.
        var customer = Data.DemoData.Customer(customerId);
        var severity = TriagePolicy.Severity(customer, signals);

        var draft = new TriageDraft
        {
            Signals = signals,
            Category = signals.Category,
            Severity = severity,
            Team = TriagePolicy.Team(signals),
            FirstResponseMinutes = (int)TriagePolicy.FirstResponseTarget(customer.Plan, severity).TotalMinutes,
            RefundProposed = TriagePolicy.Refund(customer, signals).Proposed,
            RefundAmountUsd = TriagePolicy.Refund(customer, signals).AmountUsd,
            NeedsHumanApproval = true,
            Rationale = signals.Summary,
        };

        return new SimulatedTicketPlan(customerId, signals, tools, specialist, draft);
    }
}
