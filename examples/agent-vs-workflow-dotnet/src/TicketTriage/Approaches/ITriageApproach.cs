using TicketTriage.Domain;

namespace TicketTriage.Approaches;

public interface ITriageApproach
{
    string Name { get; }

    Task<TriageOutcome> TriageAsync(SupportTicket ticket, CancellationToken cancellationToken = default);
}
