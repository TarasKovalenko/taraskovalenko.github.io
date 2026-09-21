using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// Stands in for the authorization server during local development only.
// It prints one access token to stdout so a shell or an MCP client can use it.
// Nothing here belongs in the resource server itself.

var arguments = ParseArguments(args);
if (arguments.ContainsKey("help") || arguments.ContainsKey("h"))
{
    PrintUsage();
    return 0;
}

var settingsPath = arguments.GetValueOrDefault("settings")
    ?? FindDevelopmentSettings();

if (settingsPath is null)
{
    Console.Error.WriteLine(
        "Could not find src/ExpenseMcp/appsettings.Development.json. Pass --settings <path>.");
    return 1;
}

DevelopmentSettings settings;
try
{
    settings = DevelopmentSettings.Load(settingsPath);
}
catch (Exception exception) when (exception is JsonException or KeyNotFoundException)
{
    Console.Error.WriteLine($"{settingsPath} is not a usable development configuration: {exception.Message}");
    return 1;
}

var clientId = arguments.GetValueOrDefault("client") ?? "finance-admin";
var scope = arguments.GetValueOrDefault("scope") ?? "expenses.read expenses.approve";
var subject = arguments.GetValueOrDefault("sub") ?? "user-42";
var tenantId = arguments.GetValueOrDefault("tenant") ?? "tenant-a";
var audience = arguments.GetValueOrDefault("audience") ?? settings.Audience;

if (!int.TryParse(
        arguments.GetValueOrDefault("minutes") ?? "15",
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var minutes) || minutes <= 0)
{
    Console.Error.WriteLine("--minutes must be a positive whole number.");
    return 1;
}

var now = DateTime.UtcNow;
var descriptor = new SecurityTokenDescriptor
{
    Issuer = settings.Issuer,
    Audience = audience,
    NotBefore = now.AddMinutes(-1),
    Expires = now.AddMinutes(minutes),
    IssuedAt = now,
    Subject = new ClaimsIdentity(
    [
        new Claim("sub", subject),
        new Claim("client_id", clientId),
        new Claim("tenant_id", tenantId),
        new Claim("scope", scope),
        new Claim("jti", Guid.NewGuid().ToString("N"))
    ]),
    SigningCredentials = new SigningCredentials(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
        SecurityAlgorithms.HmacSha256)
};

Console.WriteLine(new JsonWebTokenHandler().CreateToken(descriptor));
return 0;

static Dictionary<string, string> ParseArguments(string[] args)
{
    var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < args.Length; index++)
    {
        if (!args[index].StartsWith("--", StringComparison.Ordinal) &&
            !args[index].StartsWith('-'))
        {
            continue;
        }

        var name = args[index].TrimStart('-');
        var hasValue = index + 1 < args.Length &&
                       !args[index + 1].StartsWith("--", StringComparison.Ordinal);
        parsed[name] = hasValue ? args[++index] : string.Empty;
    }

    return parsed;
}

static string? FindDevelopmentSettings()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        var candidate = Path.Combine(
            directory.FullName, "src", "ExpenseMcp", "appsettings.Development.json");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    return null;
}

static void PrintUsage() => Console.WriteLine(
    """
    Issues a development access token for the local Expense MCP server.

      --client    OAuth client_id           (default: finance-admin)
      --scope     space separated scopes    (default: expenses.read expenses.approve)
      --sub       subject claim             (default: user-42)
      --tenant    tenant_id claim           (default: tenant-a)
      --audience  aud claim                 (default: from appsettings.Development.json)
      --minutes   lifetime in minutes       (default: 15)
      --settings  path to appsettings.Development.json

    Example:
      dotnet run --project tools/DevToken -- --client finance-desktop --scope expenses.read
    """);

internal sealed record DevelopmentSettings(string Issuer, string Audience, string SigningKey)
{
    public static DevelopmentSettings Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var authentication = document.RootElement.GetProperty("Authentication");

        return new DevelopmentSettings(
            Read(authentication, "Issuer"),
            Read(authentication, "Audience"),
            Read(authentication, "LocalSigningKey"));
    }

    private static string Read(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.GetString() is { Length: > 0 } text
            ? text
            : throw new KeyNotFoundException($"Authentication:{name} is missing.");
}
