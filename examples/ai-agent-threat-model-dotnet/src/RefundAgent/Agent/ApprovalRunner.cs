using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace RefundAgent.Agent;

public sealed record ApprovalDecision(bool Approved, string Reason);

public static class ApprovalRunner
{
    public static async Task<AgentResponse> RunAsync(
        AIAgent agent,
        string prompt,
        Func<ToolCallContent, Task<ApprovalDecision>> requestApproval)
    {
        AgentSession session = await agent.CreateSessionAsync();
        AgentResponse response = await agent.RunAsync(prompt, session);

        while (true)
        {
            ToolApprovalRequestContent[] approvals = response.Messages
                .SelectMany(message => message.Contents)
                .OfType<ToolApprovalRequestContent>()
                .ToArray();

            if (approvals.Length == 0)
            {
                return response;
            }

            var responseContents = new List<AIContent>(approvals.Length);

            foreach (ToolApprovalRequestContent approval in approvals)
            {
                ApprovalDecision decision =
                    await requestApproval(approval.ToolCall);

                responseContents.Add(
                    approval.CreateResponse(
                        decision.Approved,
                        decision.Reason));
            }

            response = await agent.RunAsync(
                new ChatMessage(ChatRole.User, responseContents),
                session);
        }
    }
}

