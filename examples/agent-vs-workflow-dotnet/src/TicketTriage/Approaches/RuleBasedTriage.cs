using System.Diagnostics;
using TicketTriage.Data;
using TicketTriage.Domain;
using TicketTriage.Policy;

namespace TicketTriage.Approaches;

/// <summary>Approach 1: a function. No model, no network, no non-determinism.</summary>
public sealed class RuleBasedTriage : ITriageApproach
{
    public string Name => "1. Function";

    public Task<TriageOutcome> TriageAsync(SupportTicket ticket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var started = Stopwatch.GetTimestamp();

        var signals = KeywordRules.Extract(ticket);
        var customer = DemoData.Customer(ticket.CustomerId);
        var decision = TriagePolicy.Decide(ticket, customer, signals);

        var outcome = new TriageOutcome(
            decision,
            TriageCost.Free with { Elapsed = Stopwatch.GetElapsedTime(started) },
            ["keyword extraction", "policy"]);

        return Task.FromResult(outcome);
    }
}
