using System.ComponentModel;

namespace TicketTriage.Domain;

public enum TicketCategory
{
    Unknown,
    Billing,
    TechnicalIssue,
    AccessRequest,
    Other,
}

/// <summary>
/// Facts read out of the customer's text. Every field answers "what does the message say?",
/// never "what should we do about it?". Deciding is the policy's job.
/// </summary>
public sealed record TicketSignals
{
    [Description("Best matching category for the message.")]
    public TicketCategory Category { get; init; } = TicketCategory.Unknown;

    [Description("The customer says the product or a feature is completely unusable right now.")]
    public bool ServiceUnavailable { get; init; }

    [Description("The customer says more than one person or a whole team is affected.")]
    public bool AffectsMultipleUsers { get; init; }

    [Description("The customer reports a failed, duplicated, or unexpected charge.")]
    public bool PaymentProblem { get; init; }

    [Description("The customer explicitly asks for money back.")]
    public bool RefundRequested { get; init; }

    [Description("The message mentions leaked credentials, unauthorized access, or another security concern.")]
    public bool SecurityConcern { get; init; }

    [Description("Product area mentioned by the customer, or 'unknown'.")]
    public string ProductArea { get; init; } = "unknown";

    [Description("One sentence, factual, no advice.")]
    public string Summary { get; init; } = string.Empty;
}
