using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using RefundAgent.Agent;
using RefundAgent.Domain;
using RefundAgent.Infrastructure;

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
{
    PrintHelp();
    return;
}

string endpointValue = GetRequiredEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
string deployment = GetRequiredEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
string credentialMode =
    Environment.GetEnvironmentVariable("AZURE_CREDENTIAL_MODE")
    ?? "azure-cli";

if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out Uri? endpoint)
    || endpoint.Scheme != Uri.UriSchemeHttps)
{
    throw new InvalidOperationException(
        "AZURE_OPENAI_ENDPOINT must be an absolute HTTPS URI.");
}

TokenCredential credential = credentialMode.ToLowerInvariant() switch
{
    "azure-cli" => new AzureCliCredential(),
    "managed-identity" =>
        new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned),
    _ => throw new InvalidOperationException(
        "AZURE_CREDENTIAL_MODE must be 'azure-cli' or 'managed-identity'."),
};

IChatClient chatClient = new AzureOpenAIClient(endpoint, credential)
    .GetChatClient(deployment)
    .AsIChatClient();

var executionContext = new AgentExecutionContext(
    UserId: SampleData.Order.UserId,
    TenantId: SampleData.Order.TenantId,
    Scopes: new HashSet<string>(StringComparer.Ordinal)
    {
        "refunds.read",
        "refunds.write",
    },
    CorrelationId: Guid.NewGuid().ToString("N"));

var repository = new InMemoryOrderRepository([SampleData.Order]);
var gateway = new InMemoryRefundGateway();
var tools = new RefundTools(executionContext, repository, gateway);
AIAgent agent = RefundAgentFactory.Create(chatClient, tools);

string prompt = args.Length > 0
    ? string.Join(' ', args)
    : ReadPrompt();

AgentResponse response = await ApprovalRunner.RunAsync(
    agent,
    prompt,
    ConsoleApproval.RequestAsync);

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("Agent response");
Console.ResetColor();
Console.WriteLine(response.Text);

static string ReadPrompt()
{
    Console.WriteLine("Refund agent demo");
    Console.WriteLine($"Sample order: {SampleData.OrderId}");
    Console.WriteLine("Refundable amount: 250.00 USD");
    Console.WriteLine();
    Console.Write("Request: ");

    return Console.ReadLine() is { Length: > 0 } prompt
        ? prompt
        : $"Preview a 25 USD refund for order {SampleData.OrderId}.";
}

static string GetRequiredEnvironmentVariable(string name)
{
    return Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"Required environment variable '{name}' is missing.");
}

static void PrintHelp()
{
    Console.WriteLine(
        """
        Refund Agent sample

        Required environment variables:
          AZURE_OPENAI_ENDPOINT
          AZURE_OPENAI_DEPLOYMENT

        Optional:
          AZURE_CREDENTIAL_MODE=azure-cli|managed-identity

        Usage:
          dotnet run --project src/RefundAgent
          dotnet run --project src/RefundAgent -- "Preview a 25 USD refund."
        """);
}
