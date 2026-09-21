using System.ClientModel;
using System.Globalization;
using Microsoft.Extensions.AI;
using OpenAI;
using TicketTriage.Approaches;
using TicketTriage.Data;
using TicketTriage.Domain;
using TicketTriage.Simulation;

var live = args.Contains("--live", StringComparer.Ordinal);
var adversarial = args.Contains("--adversarial", StringComparer.Ordinal);
var ticketFilter = args.FirstOrDefault(a => a.StartsWith("T-", StringComparison.Ordinal));

using IChatClient chatClient = live ? CreateLiveClient() : new SimulatedChatClient(adversarial ? SimulationScript.Adversarial : SimulationScript.Reasonable);

ITriageApproach[] approaches =
[
    new RuleBasedTriage(),
    new WorkflowTriage(chatClient),
    new AgentTriage(chatClient),
    new MultiAgentTriage(chatClient),
];

var tickets = ticketFilter is null ? DemoData.Tickets : [DemoData.Ticket(ticketFilter)];

Console.WriteLine(live ? "Model: live provider" : $"Model: simulated ({(adversarial ? "adversarial" : "reasonable")} script)");
Console.WriteLine();

foreach (var ticket in tickets)
{
    var customer = DemoData.Customer(ticket.CustomerId);
    Console.WriteLine($"{ticket.Id}  {customer.Name} ({customer.Plan})  {ticket.Subject}");
    Console.WriteLine($"{"Approach",-16}{"Sev",-5}{"Team",-12}{"Response",-10}{"Refund",-10}{"Model",-7}{"Tools",-7}{"Tokens",-9}{"Elapsed",-9}");

    foreach (var approach in approaches)
    {
        var outcome = await approach.TriageAsync(ticket).ConfigureAwait(false);
        var d = outcome.Decision;
        var refund = d.RefundProposed ? d.RefundAmountUsd.ToString("0", CultureInfo.InvariantCulture) + " USD" : "-";
        var tokens = outcome.Cost.InputTokens + outcome.Cost.OutputTokens;

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{approach.Name,-16}{d.Severity,-5}{d.Team,-12}{Format(d.FirstResponseTarget),-10}{refund,-10}{outcome.Cost.ModelCalls,-7}{outcome.Cost.ToolCalls,-7}{tokens,-9}{outcome.Cost.Elapsed.TotalMilliseconds,-9:0}"));

        if (Environment.GetEnvironmentVariable("TRIAGE_TRACE") == "1")
        {
            foreach (var step in outcome.Trace)
            {
                Console.WriteLine($"{"",-16}- {step}");
            }
        }

        if (outcome.Trace.Any(t => t.StartsWith("policy check:", StringComparison.Ordinal) && !t.EndsWith("clean", StringComparison.Ordinal)))
        {
            Console.WriteLine($"{"",-16}blocked: {string.Join(" | ", outcome.Trace.Where(t => t.StartsWith("policy check:", StringComparison.Ordinal)))}");
        }
    }

    Console.WriteLine();
}

static string Format(TimeSpan value) =>
    value.TotalHours >= 1
        ? string.Create(CultureInfo.InvariantCulture, $"{value.TotalHours:0.#}h")
        : string.Create(CultureInfo.InvariantCulture, $"{value.TotalMinutes:0}m");

static IChatClient CreateLiveClient()
{
    var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ?? throw new InvalidOperationException("Set OPENAI_API_KEY to run with --live.");
    var model = Environment.GetEnvironmentVariable("TRIAGE_MODEL") ?? "gpt-4.1-mini";
    var endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");

    var options = new OpenAIClientOptions();
    if (!string.IsNullOrWhiteSpace(endpoint))
    {
        options.Endpoint = new Uri(endpoint);
    }

    return new OpenAIClient(new ApiKeyCredential(apiKey), options)
        .GetChatClient(model)
        .AsIChatClient();
}
