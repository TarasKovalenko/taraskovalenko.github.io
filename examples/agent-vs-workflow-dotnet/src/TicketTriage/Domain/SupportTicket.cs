namespace TicketTriage.Domain;

/// <summary>An inbound support message, exactly as the customer wrote it.</summary>
public sealed record SupportTicket(
    string Id,
    string CustomerId,
    string Subject,
    string Body,
    DateTimeOffset ReceivedAt);
