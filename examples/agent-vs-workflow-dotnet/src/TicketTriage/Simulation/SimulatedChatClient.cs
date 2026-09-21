using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace TicketTriage.Simulation;

/// <summary>
/// A stand-in for a real model so the sample runs offline and the tests stay deterministic.
/// It reproduces the shape of model behavior that matters here: structured output, tool calls,
/// and handoffs. It does not reproduce model quality, and it never surprises you.
/// </summary>
public sealed partial class SimulatedChatClient(SimulationScript script) : IChatClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SimulationScript _script = script;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        cancellationToken.ThrowIfCancellationRequested();

        var history = messages.ToList();
        var transcript = string.Join("\n", history.Select(m => m.Text));
        var plan = _script.For(TicketId(transcript));

        var pending = PendingCalls(history, options, plan);
        var response = pending.Count > 0
            ? new ChatResponse(new ChatMessage(ChatRole.Assistant, [.. pending]))
            : new ChatResponse(new ChatMessage(ChatRole.Assistant, FinalText(options, plan)));

        response.Usage = EstimateUsage(transcript, response.Text);
        response.ModelId = "simulated-model";
        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
    }

    /// <summary>Tools the scripted model still wants to call, in the order the script lists them.</summary>
    private static List<FunctionCallContent> PendingCalls(
        List<ChatMessage> history,
        ChatOptions? options,
        SimulatedTicketPlan plan)
    {
        var offered = options?.Tools?.OfType<AIFunctionDeclaration>().ToList() ?? [];
        if (offered.Count == 0)
        {
            return [];
        }

        var alreadyCalled = history
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A handoff tool means this run is part of a multi-agent workflow: pass the ticket on first.
        var handoff = offered.FirstOrDefault(t =>
            t.Name.Contains("handoff", StringComparison.OrdinalIgnoreCase)
            && (t.Name.Contains(plan.Specialist, StringComparison.OrdinalIgnoreCase)
                || t.Description.Contains(plan.Specialist, StringComparison.OrdinalIgnoreCase)));

        if (handoff is not null && !alreadyCalled.Contains(handoff.Name))
        {
            return [new FunctionCallContent(NextCallId(alreadyCalled.Count), handoff.Name, Arguments(handoff, plan))];
        }

        var wanted = plan.Tools
            .Select(name => offered.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .OfType<AIFunctionDeclaration>()
            .Where(t => !alreadyCalled.Contains(t.Name))
            .ToList();

        return [.. wanted.Select((t, i) => new FunctionCallContent(NextCallId(alreadyCalled.Count + i), t.Name, Arguments(t, plan)))];
    }

    private static string FinalText(ChatOptions? options, SimulatedTicketPlan plan)
    {
        var schema = (options?.ResponseFormat as ChatResponseFormatJson)?.Schema?.ToString() ?? string.Empty;
        var wantsDecision = schema.Contains("\"team\"", StringComparison.OrdinalIgnoreCase);

        return wantsDecision || schema.Length == 0
            ? JsonSerializer.Serialize(plan.Draft, Json)
            : JsonSerializer.Serialize(plan.Signals, Json);
    }

    private static Dictionary<string, object?> Arguments(AIFunctionDeclaration tool, SimulatedTicketPlan plan)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (!tool.JsonSchema.TryGetProperty("properties", out var properties))
        {
            return arguments;
        }

        foreach (var property in properties.EnumerateObject())
        {
            arguments[property.Name] = property.Name switch
            {
                "customerId" => plan.CustomerId,
                "productArea" => plan.Signals.ProductArea,
                _ => plan.Signals.Summary,
            };
        }

        return arguments;
    }

    private static UsageDetails EstimateUsage(string prompt, string completion) => new()
    {
        // Rough stand-in so the cost column has something to show. Real numbers come from the provider.
        InputTokenCount = Math.Max(1, prompt.Length / 4),
        OutputTokenCount = Math.Max(1, completion.Length / 4),
    };

    private static string NextCallId(int index) => $"sim-{index + 1}";

    private static string? TicketId(string transcript)
    {
        var match = TicketIdPattern().Match(transcript);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"T-\d{4}")]
    private static partial Regex TicketIdPattern();
}
