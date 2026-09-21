using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace ExpenseMcp.SecurityTests;

public sealed class SecurityTests
{
    [Fact]
    public async Task ProtectedResourceMetadataUsesCanonicalResource()
    {
        await using var factory = new ExpenseMcpFactory();
        using var client = factory.CreateClient();

        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync(
            "/.well-known/oauth-protected-resource",
            cancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ExpenseMcpFactory.Audience,
            document.RootElement.GetProperty("resource").GetString());
        Assert.Contains("expenses.read",
            document.RootElement.GetProperty("scopes_supported")
                .EnumerateArray()
                .Select(item => item.GetString()));
    }

    [Fact]
    public async Task MissingTokenReturnsDiscoveryChallenge()
    {
        await using var factory = new ExpenseMcpFactory();
        using var client = factory.CreateClient();
        using var request = CreateToolCall("list_expenses");

        using var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);
        var challenge = response.Headers.WwwAuthenticate.Single().ToString();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("resource_metadata=\"", challenge, StringComparison.Ordinal);
        Assert.Contains("scope=\"expenses.read\"", challenge, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenForAnotherAudienceIsRejected()
    {
        await using var factory = new ExpenseMcpFactory();
        using var client = factory.CreateClient();
        using var request = CreateToolCall("list_expenses", TestTokens.Create(
            audience: "https://files.example.com/mcp"));

        using var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingToolScopeReturnsStepUpChallenge()
    {
        await using var factory = new ExpenseMcpFactory();
        using var client = factory.CreateClient();
        using var request = CreateToolCall("approve_expense", TestTokens.Create(
            scope: "expenses.read"));

        using var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);
        var challenge = response.Headers.WwwAuthenticate.Single().ToString();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("error=\"insufficient_scope\"", challenge, StringComparison.Ordinal);
        Assert.Contains("scope=\"expenses.approve\"", challenge, StringComparison.Ordinal);
        Assert.Contains(
            "resource_metadata=\"http://localhost/.well-known/oauth-protected-resource\"",
            challenge,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnconfiguredToolIsDeniedByDefault()
    {
        await using var factory = new ExpenseMcpFactory();
        using var client = factory.CreateClient();
        using var request = CreateToolCall("new_unconfigured_tool", TestTokens.Create());

        using var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ClientCannotUseScopeItWasNotEntitledTo()
    {
        await using var factory = new ExpenseMcpFactory();
        using var client = factory.CreateClient();
        using var request = CreateToolCall("approve_expense", TestTokens.Create(
            clientId: "finance-desktop",
            scope: "expenses.read expenses.approve"));

        using var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuthorizedToolCallIsExecutedAndAuditedWithoutArguments()
    {
        await using var factory = new ExpenseMcpFactory();
        using var client = factory.CreateClient();
        var token = TestTokens.Create();
        using var request = CreateToolCall("list_expenses", token);

        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 but received {(int)response.StatusCode}: {body}");
        Assert.Contains("Train to client workshop", body, StringComparison.Ordinal);

        var audit = Assert.Single(factory.AuditSink.Events);
        Assert.Equal("user-42", audit.Subject);
        Assert.Equal("finance-admin", audit.ClientId);
        Assert.Equal("list_expenses", audit.Tool);
        Assert.Equal("allowed", audit.Outcome);
        Assert.DoesNotContain("Train to client workshop", JsonSerializer.Serialize(audit),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RateLimitIsPartitionedAndReturnsRetryAfter()
    {
        await using var factory = new ExpenseMcpFactory(permitLimit: 1);
        using var client = factory.CreateClient();

        var cancellationToken = TestContext.Current.CancellationToken;
        using var first = await client.SendAsync(
            CreateToolCall("list_expenses", TestTokens.Create()),
            cancellationToken);
        using var second = await client.SendAsync(
            CreateToolCall("list_expenses", TestTokens.Create()),
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.True(second.Headers.Contains(HeaderNames.RetryAfter));
    }

    private static HttpRequestMessage CreateToolCall(string toolName, string? token = null)
    {
        var arguments = toolName == "approve_expense"
            ? ",\"arguments\":{\"expenseId\":\"7fce98d1-d91e-44b0-aab7-440af78d18af\"}"
            : ",\"arguments\":{}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                $"{{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{{\"name\":\"{toolName}\"{arguments},\"_meta\":{{\"io.modelcontextprotocol/protocolVersion\":\"2026-07-28\",\"io.modelcontextprotocol/clientInfo\":{{\"name\":\"security-tests\",\"version\":\"1.0\"}},\"io.modelcontextprotocol/clientCapabilities\":{{}}}}}}}}",
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2026-07-28");
        request.Headers.TryAddWithoutValidation("Mcp-Method", "tools/call");
        request.Headers.TryAddWithoutValidation("Mcp-Name", toolName);

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }
}
