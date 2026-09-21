namespace TicketTriage.Domain;

public enum SupportPlan
{
    Free,
    Business,
    Enterprise,
}

public sealed record CustomerAccount(
    string Id,
    string Name,
    SupportPlan Plan,
    decimal MonthlySpendUsd,
    int SeatCount);
