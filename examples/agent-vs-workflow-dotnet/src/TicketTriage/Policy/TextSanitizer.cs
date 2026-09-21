using System.Text.RegularExpressions;

namespace TicketTriage.Policy;

/// <summary>Removes the obvious personal data before any text leaves the process.</summary>
public static partial class TextSanitizer
{
    public static string Redact(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var result = EmailPattern().Replace(text, "[email]");
        result = CardPattern().Replace(result, "[card]");
        return result;
    }

    [GeneratedRegex(@"[\w\.\-\+]+@[\w\-]+\.[\w\.\-]+", RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\b(?:\d[ \-]*?){13,19}\b")]
    private static partial Regex CardPattern();
}
