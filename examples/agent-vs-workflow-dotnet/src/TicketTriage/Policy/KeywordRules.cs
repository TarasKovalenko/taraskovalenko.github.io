using TicketTriage.Domain;

namespace TicketTriage.Policy;

/// <summary>
/// Signal extraction without a model: substring matching over a hand-written vocabulary.
/// Cheap, instant, and fully predictable. It only sees the words it was told about.
/// </summary>
public static class KeywordRules
{
    private static readonly string[] BillingWords = ["invoice", "charge", "charged", "billing", "payment", "card", "subscription", "refund"];
    private static readonly string[] TechnicalWords = ["error", "500", "503", "timeout", "crash", "bug", "failed", "failing", "broken"];
    private static readonly string[] AccessWords = ["password", "sso", "login", "log in", "sign in", "access", "permission", "invite"];
    private static readonly string[] OutageWords = ["down", "outage", "unavailable", "cannot use", "can't use", "unusable"];
    private static readonly string[] MultiUserWords = ["everyone", "all users", "whole team", "our team", "nobody", "no one"];
    private static readonly string[] PaymentProblemWords = ["charged twice", "double charge", "duplicate charge", "payment failed", "declined", "overcharged"];
    private static readonly string[] RefundWords = ["refund", "money back", "chargeback", "reimburse"];
    private static readonly string[] SecurityWords = ["breach", "hacked", "leaked", "unauthorized", "phishing", "compromised"];

    public static TicketSignals Extract(SupportTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var text = $"{ticket.Subject}\n{ticket.Body}".ToLowerInvariant();

        var billing = Contains(text, BillingWords);
        var technical = Contains(text, TechnicalWords);
        var access = Contains(text, AccessWords);

        var category = (billing, technical, access) switch
        {
            (true, _, _) => TicketCategory.Billing,
            (_, true, _) => TicketCategory.TechnicalIssue,
            (_, _, true) => TicketCategory.AccessRequest,
            _ => TicketCategory.Other,
        };

        return new TicketSignals
        {
            Category = category,
            ServiceUnavailable = Contains(text, OutageWords),
            AffectsMultipleUsers = Contains(text, MultiUserWords),
            PaymentProblem = Contains(text, PaymentProblemWords),
            RefundRequested = Contains(text, RefundWords),
            SecurityConcern = Contains(text, SecurityWords),
            ProductArea = "unknown",
            Summary = ticket.Subject,
        };
    }

    private static bool Contains(string text, string[] words) =>
        Array.Exists(words, w => text.Contains(w, StringComparison.Ordinal));
}
