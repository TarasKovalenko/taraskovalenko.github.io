namespace TicketTriage.Domain;

public enum TicketSeverity
{
    S1 = 1,
    S2 = 2,
    S3 = 3,
    S4 = 4,
}

public sealed record TriageDecision(
    string TicketId,
    TicketCategory Category,
    TicketSeverity Severity,
    string Team,
    TimeSpan FirstResponseTarget,
    bool RefundProposed,
    decimal RefundAmountUsd,
    bool NeedsHumanApproval,
    string Rationale);
