using System.ComponentModel;

namespace TicketTriage.Domain;

/// <summary>
/// What an agent proposes on its own: the facts it believes plus the decision it made.
/// Reporting both is what makes the proposal checkable against policy.
/// </summary>
public sealed record TriageDraft
{
    [Description("Facts the agent read out of the ticket.")]
    public TicketSignals Signals { get; init; } = new();

    [Description("Category of the ticket.")]
    public TicketCategory Category { get; init; } = TicketCategory.Unknown;

    [Description("Severity from S1 (most urgent) to S4.")]
    public TicketSeverity Severity { get; init; } = TicketSeverity.S4;

    [Description("Team that should handle it: billing, platform, identity, security, or frontline.")]
    public string Team { get; init; } = "frontline";

    [Description("First response target in minutes.")]
    public int FirstResponseMinutes { get; init; }

    [Description("True if a refund should be proposed.")]
    public bool RefundProposed { get; init; }

    [Description("Refund amount in USD, 0 when no refund is proposed.")]
    public decimal RefundAmountUsd { get; init; }

    [Description("True if a human must approve before anything is sent to the customer.")]
    public bool NeedsHumanApproval { get; init; }

    [Description("One or two sentences explaining the decision.")]
    public string Rationale { get; init; } = string.Empty;

    public TriageDecision ToDecision(string ticketId) => new(
        TicketId: ticketId,
        Category: Category,
        Severity: Severity,
        Team: Team,
        FirstResponseTarget: TimeSpan.FromMinutes(FirstResponseMinutes),
        RefundProposed: RefundProposed,
        RefundAmountUsd: RefundAmountUsd,
        NeedsHumanApproval: NeedsHumanApproval,
        Rationale: Rationale);
}
