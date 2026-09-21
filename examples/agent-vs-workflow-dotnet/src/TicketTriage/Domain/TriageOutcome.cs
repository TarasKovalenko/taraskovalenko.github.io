namespace TicketTriage.Domain;

/// <summary>What a run cost, in units that stay comparable across the four approaches.</summary>
public sealed record TriageCost(
    int ModelCalls,
    int ToolCalls,
    long InputTokens,
    long OutputTokens,
    TimeSpan Elapsed)
{
    public static readonly TriageCost Free = new(0, 0, 0, 0, TimeSpan.Zero);
}

public sealed record TriageOutcome(TriageDecision Decision, TriageCost Cost, IReadOnlyList<string> Trace);
