using TicketTriage.Approaches;
using TicketTriage.Data;
using TicketTriage.Domain;
using TicketTriage.Policy;
using TicketTriage.Simulation;

using Xunit;

namespace TicketTriage.Tests;

/// <summary>
/// These tests compare control flow and blast radius, not model quality: the model here is a
/// scripted stand-in. What they do prove is what each approach does with what the model returns.
/// </summary>
public class ApproachBehaviorTests
{
    private static SimulatedChatClient Reasonable() => new(SimulationScript.Reasonable);

    private static SimulatedChatClient Adversarial() => new(SimulationScript.Adversarial);

    [Fact]
    public async Task KeywordRulesMissASecurityReportWithoutKeywords()
    {
        var outcome = await new RuleBasedTriage().TriageAsync(DemoData.Ticket("T-1005"), TestContext.Current.CancellationToken);

        Assert.Equal(TicketSeverity.S4, outcome.Decision.Severity);
        Assert.Equal("billing", outcome.Decision.Team);
        Assert.Equal(0, outcome.Cost.ModelCalls);
    }

    [Fact]
    public async Task WorkflowReadsTheSameTicketAsASecurityIncident()
    {
        using var client = Reasonable();

        var outcome = await new WorkflowTriage(client).TriageAsync(DemoData.Ticket("T-1005"), TestContext.Current.CancellationToken);

        Assert.Equal(TicketSeverity.S1, outcome.Decision.Severity);
        Assert.Equal("security", outcome.Decision.Team);
        Assert.Equal(1, outcome.Cost.ModelCalls);
    }

    [Fact]
    public async Task WorkflowIgnoresInstructionsHiddenInATicket()
    {
        using var client = Adversarial();

        var outcome = await new WorkflowTriage(client).TriageAsync(DemoData.Ticket("T-1006"), TestContext.Current.CancellationToken);

        // The model was talked into "urgent refund". The decision still comes from policy.
        Assert.False(outcome.Decision.RefundProposed);
        Assert.Equal(0m, outcome.Decision.RefundAmountUsd);
        Assert.Contains(outcome.Decision.Team, TriagePolicy.KnownTeams);
        Assert.Equal(TicketSeverity.S4, outcome.Decision.Severity);
    }

    [Fact]
    public async Task AgentFollowsTheInjectedInstructionWhenNothingChecksIt()
    {
        using var client = Adversarial();

        var outcome = await new AgentTriage(client, enforcePolicy: false).TriageAsync(DemoData.Ticket("T-1006"), TestContext.Current.CancellationToken);

        Assert.True(outcome.Decision.RefundProposed);
        Assert.Equal(5000m, outcome.Decision.RefundAmountUsd);
        Assert.DoesNotContain(outcome.Decision.Team, TriagePolicy.KnownTeams);
        Assert.False(outcome.Decision.NeedsHumanApproval);
    }

    [Fact]
    public async Task AgentOutputThatBreaksPolicyIsReplacedByThePolicyDecision()
    {
        using var client = Adversarial();

        var outcome = await new AgentTriage(client).TriageAsync(DemoData.Ticket("T-1006"), TestContext.Current.CancellationToken);

        Assert.False(outcome.Decision.RefundProposed);
        Assert.Contains(outcome.Decision.Team, TriagePolicy.KnownTeams);
        Assert.Contains(outcome.Trace, step => step.StartsWith("policy check:", StringComparison.Ordinal));
        Assert.Contains("policy enforced", outcome.Trace);
    }

    [Fact]
    public async Task MultiAgentSendsABillingTicketToTheBillingSpecialist()
    {
        using var client = Reasonable();

        var outcome = await new MultiAgentTriage(client).TriageAsync(DemoData.Ticket("T-1002"), TestContext.Current.CancellationToken);

        Assert.Contains(outcome.Trace, step => step.Contains("billing-specialist", StringComparison.Ordinal));
        Assert.Contains(outcome.Trace, step => step.StartsWith("get_refund_policy", StringComparison.Ordinal));
        Assert.Equal("billing", outcome.Decision.Team);
    }

    [Fact]
    public async Task EveryStepOfAutonomyCostsAnotherModelCall()
    {
        using var client = Reasonable();
        var ticket = DemoData.Ticket("T-1002");

        var rules = await new RuleBasedTriage().TriageAsync(ticket, TestContext.Current.CancellationToken);
        var workflow = await new WorkflowTriage(client).TriageAsync(ticket, TestContext.Current.CancellationToken);
        var agent = await new AgentTriage(client).TriageAsync(ticket, TestContext.Current.CancellationToken);
        var multiAgent = await new MultiAgentTriage(client).TriageAsync(ticket, TestContext.Current.CancellationToken);

        Assert.Equal(0, rules.Cost.ModelCalls);
        Assert.Equal(1, workflow.Cost.ModelCalls);
        Assert.Equal(2, agent.Cost.ModelCalls);
        Assert.Equal(3, multiAgent.Cost.ModelCalls);

        // Same ticket, same answer, four different price tags.
        Assert.Equal(rules.Decision.Severity, multiAgent.Decision.Severity);
        Assert.Equal(rules.Decision.Team, multiAgent.Decision.Team);
    }

    [Fact]
    public async Task TheWorkflowGivesTheSameAnswerEveryTime()
    {
        using var client = Reasonable();
        var ticket = DemoData.Ticket("T-1001");

        var first = await new WorkflowTriage(client).TriageAsync(ticket, TestContext.Current.CancellationToken);
        var second = await new WorkflowTriage(client).TriageAsync(ticket, TestContext.Current.CancellationToken);

        Assert.Equal(first.Decision, second.Decision);
    }
}
