using TicketTriage.Data;
using TicketTriage.Domain;
using TicketTriage.Policy;

using Xunit;

namespace TicketTriage.Tests;

public class TriagePolicyTests
{
    [Fact]
    public void EnterpriseOutageForSeveralUsersIsS1()
    {
        var signals = new TicketSignals
        {
            Category = TicketCategory.TechnicalIssue,
            ServiceUnavailable = true,
            AffectsMultipleUsers = true,
        };

        Assert.Equal(TicketSeverity.S1, TriagePolicy.Severity(DemoData.Customer("acme"), signals));
    }

    [Fact]
    public void FreePlanNeverGetsARefund()
    {
        var signals = new TicketSignals
        {
            Category = TicketCategory.Billing,
            PaymentProblem = true,
            RefundRequested = true,
        };

        var (proposed, amount) = TriagePolicy.Refund(DemoData.Customer("lumen"), signals);

        Assert.False(proposed);
        Assert.Equal(0m, amount);
    }

    [Fact]
    public void RefundNeverExceedsTheCap()
    {
        var signals = new TicketSignals
        {
            Category = TicketCategory.Billing,
            PaymentProblem = true,
            RefundRequested = true,
        };

        // Acme spends 4200 USD a month, well above the cap.
        var (proposed, amount) = TriagePolicy.Refund(DemoData.Customer("acme"), signals);

        Assert.True(proposed);
        Assert.Equal(TriagePolicy.RefundCapUsd, amount);
    }

    [Fact]
    public void SecurityConcernOutranksTheCategory()
    {
        var signals = new TicketSignals { Category = TicketCategory.Billing, SecurityConcern = true };

        Assert.Equal("security", TriagePolicy.Team(signals));
        Assert.Equal(TicketSeverity.S1, TriagePolicy.Severity(DemoData.Customer("nova"), signals));
    }
}
