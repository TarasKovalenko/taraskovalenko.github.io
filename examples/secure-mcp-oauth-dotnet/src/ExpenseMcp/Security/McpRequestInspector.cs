using System.Text.Json;

namespace ExpenseMcp.Security;

public static class McpRequestInspector
{
    private const long MaximumBodySize = 64 * 1024;

    public static async Task<string?> ReadToolNameAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!HttpMethods.IsPost(request.Method) ||
            !request.Path.Equals("/mcp", StringComparison.OrdinalIgnoreCase) ||
            request.ContentLength > MaximumBodySize)
        {
            return null;
        }

        request.EnableBuffering(bufferThreshold: 16 * 1024, bufferLimit: MaximumBodySize);

        try
        {
            using var document = await JsonDocument.ParseAsync(
                request.Body,
                new JsonDocumentOptions { MaxDepth = 16 },
                cancellationToken);

            var root = document.RootElement;
            if (!root.TryGetProperty("method", out var method) || method.GetString() != "tools/call" ||
                !root.TryGetProperty("params", out var parameters) ||
                !parameters.TryGetProperty("name", out var name))
            {
                return null;
            }

            return name.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }
}
