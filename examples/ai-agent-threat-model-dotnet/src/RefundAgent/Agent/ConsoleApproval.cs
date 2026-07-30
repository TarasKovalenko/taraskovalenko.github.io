using System.Text.Json;
using Microsoft.Extensions.AI;

namespace RefundAgent.Agent;

public static class ConsoleApproval
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static Task<ApprovalDecision> RequestAsync(ToolCallContent toolCall)
    {
        if (toolCall is not FunctionCallContent functionCall)
        {
            return Task.FromResult(
                new ApprovalDecision(
                    Approved: false,
                    $"Unsupported tool-call type: {toolCall.GetType().Name}."));
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine();
        Console.WriteLine("A state-changing tool call requires approval.");
        Console.ResetColor();
        Console.WriteLine($"Tool: {functionCall.Name}");
        Console.WriteLine($"Call ID: {functionCall.CallId}");
        Console.WriteLine("Arguments:");
        Console.WriteLine(
            JsonSerializer.Serialize(functionCall.Arguments, JsonOptions));
        Console.Write("Approve this exact operation? [y/N]: ");

        string? answer = Console.ReadLine();
        bool approved = string.Equals(
            answer?.Trim(),
            "y",
            StringComparison.OrdinalIgnoreCase);

        string reason = approved
            ? "Approved by the interactive console user."
            : "Rejected by the interactive console user.";

        return Task.FromResult(new ApprovalDecision(approved, reason));
    }
}
