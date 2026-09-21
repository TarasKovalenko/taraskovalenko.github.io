using Microsoft.Extensions.AI;

namespace TicketTriage.Tools;

/// <summary>Counts what a run actually spends: one entry per request that reaches the model.</summary>
public sealed class CountingChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    private int _calls;
    private long _inputTokens;
    private long _outputTokens;

    public int Calls => Volatile.Read(ref _calls);

    public long InputTokens => Interlocked.Read(ref _inputTokens);

    public long OutputTokens => Interlocked.Read(ref _outputTokens);

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

        Interlocked.Increment(ref _calls);
        Interlocked.Add(ref _inputTokens, response.Usage?.InputTokenCount ?? 0);
        Interlocked.Add(ref _outputTokens, response.Usage?.OutputTokenCount ?? 0);

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<ChatResponseUpdate> updates = [];

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            updates.Add(update);
            yield return update;
        }

        var usage = updates.ToChatResponse().Usage;

        Interlocked.Increment(ref _calls);
        Interlocked.Add(ref _inputTokens, usage?.InputTokenCount ?? 0);
        Interlocked.Add(ref _outputTokens, usage?.OutputTokenCount ?? 0);
    }
}
