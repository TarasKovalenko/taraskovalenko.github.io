using TicketTriage.Domain;

namespace TicketTriage.Policy;

/// <summary>
/// The part of triage that must never be guessed: severity, routing, response targets,
/// and the refund cap. Every approach in this sample ends up here, or is checked against it.
/// </summary>
public static class TriagePolicy
{
    public const decimal RefundCapUsd = 500m;

    public static readonly IReadOnlySet<string> KnownTeams =
        new HashSet<string>(StringComparer.Ordinal) { "billing", "platform", "identity", "security", "frontline" };

    public static TriageDecision Decide(SupportTicket ticket, CustomerAccount customer, TicketSignals signals)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(signals);

        var severity = Severity(customer, signals);
        var team = Team(signals);
        var target = FirstResponseTarget(customer.Plan, severity);
        var (refund, amount) = Refund(customer, signals);

        return new TriageDecision(
            TicketId: ticket.Id,
            Category: signals.Category,
            Severity: severity,
            Team: team,
            FirstResponseTarget: target,
            RefundProposed: refund,
            RefundAmountUsd: amount,
            NeedsHumanApproval: refund || severity == TicketSeverity.S1,
            Rationale: Rationale(customer, signals, severity, team));
    }

    public static TicketSeverity Severity(CustomerAccount customer, TicketSignals signals)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(signals);

        var baseline = signals switch
        {
            { SecurityConcern: true } => TicketSeverity.S1,
            { ServiceUnavailable: true, AffectsMultipleUsers: true } => TicketSeverity.S1,
            { ServiceUnavailable: true } => TicketSeverity.S2,
            { PaymentProblem: true } => TicketSeverity.S3,
            { Category: TicketCategory.TechnicalIssue } => TicketSeverity.S3,
            _ => TicketSeverity.S4,
        };

        // Plan changes urgency, never the facts.
        var adjusted = customer.Plan switch
        {
            SupportPlan.Enterprise => (int)baseline - 1,
            SupportPlan.Free => (int)baseline + 1,
            _ => (int)baseline,
        };

        return (TicketSeverity)Math.Clamp(adjusted, (int)TicketSeverity.S1, (int)TicketSeverity.S4);
    }

    public static string Team(TicketSignals signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        if (signals.SecurityConcern)
        {
            return "security";
        }

        return signals.Category switch
        {
            TicketCategory.Billing => "billing",
            TicketCategory.TechnicalIssue => "platform",
            TicketCategory.AccessRequest => "identity",
            _ => "frontline",
        };
    }

    public static TimeSpan FirstResponseTarget(SupportPlan plan, TicketSeverity severity) => (plan, severity) switch
    {
        (SupportPlan.Enterprise, TicketSeverity.S1) => TimeSpan.FromMinutes(15),
        (SupportPlan.Enterprise, TicketSeverity.S2) => TimeSpan.FromHours(1),
        (SupportPlan.Enterprise, TicketSeverity.S3) => TimeSpan.FromHours(4),
        (SupportPlan.Enterprise, _) => TimeSpan.FromHours(8),
        (SupportPlan.Business, TicketSeverity.S1) => TimeSpan.FromHours(1),
        (SupportPlan.Business, TicketSeverity.S2) => TimeSpan.FromHours(4),
        (SupportPlan.Business, TicketSeverity.S3) => TimeSpan.FromHours(8),
        (SupportPlan.Business, _) => TimeSpan.FromHours(24),
        (_, TicketSeverity.S1) => TimeSpan.FromHours(8),
        (_, TicketSeverity.S2) => TimeSpan.FromHours(24),
        _ => TimeSpan.FromHours(72),
    };

    public static (bool Proposed, decimal AmountUsd) Refund(CustomerAccount customer, TicketSignals signals)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(signals);

        if (!signals.RefundRequested || !signals.PaymentProblem || customer.Plan == SupportPlan.Free)
        {
            return (false, 0m);
        }

        return (true, Math.Min(customer.MonthlySpendUsd, RefundCapUsd));
    }

    /// <summary>
    /// Checks a decision that came from somewhere less predictable than <see cref="Decide"/>.
    /// Returns the violations; an empty list means the decision is inside policy.
    /// </summary>
    public static IReadOnlyList<string> Violations(TriageDecision decision, CustomerAccount customer, TicketSignals signals)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(signals);

        var problems = new List<string>();

        if (!KnownTeams.Contains(decision.Team))
        {
            problems.Add($"unknown team '{decision.Team}'");
        }

        var expectedSeverity = Severity(customer, signals);
        if (decision.Severity != expectedSeverity)
        {
            problems.Add($"severity {decision.Severity} instead of {expectedSeverity}");
        }

        var expectedTarget = FirstResponseTarget(customer.Plan, decision.Severity);
        if (decision.FirstResponseTarget != expectedTarget)
        {
            problems.Add($"response target {decision.FirstResponseTarget} instead of {expectedTarget}");
        }

        var (expectedRefund, expectedAmount) = Refund(customer, signals);
        if (decision.RefundProposed != expectedRefund)
        {
            problems.Add(expectedRefund ? "refund missing" : "refund proposed outside policy");
        }
        else if (decision.RefundAmountUsd != expectedAmount)
        {
            problems.Add($"refund {decision.RefundAmountUsd:0.##} instead of {expectedAmount:0.##}");
        }

        if (decision.RefundProposed && !decision.NeedsHumanApproval)
        {
            problems.Add("refund without human approval");
        }

        return problems;
    }

    private static string Rationale(CustomerAccount customer, TicketSignals signals, TicketSeverity severity, string team)
    {
        var reasons = new List<string>();

        if (signals.SecurityConcern)
        {
            reasons.Add("security concern reported");
        }

        if (signals.ServiceUnavailable)
        {
            reasons.Add(signals.AffectsMultipleUsers ? "service down for several users" : "service down for one user");
        }

        if (signals.PaymentProblem)
        {
            reasons.Add("payment problem reported");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("no blocking impact reported");
        }

        return $"{severity} for {customer.Plan} plan, routed to {team}: {string.Join("; ", reasons)}.";
    }
}
