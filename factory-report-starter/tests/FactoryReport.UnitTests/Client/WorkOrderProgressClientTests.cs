using FactoryReport.Client.Services.Display;

namespace FactoryReport.UnitTests.Client;

public class WorkOrderOverdueDisplayTests
{
    [Fact]
    public void GetBadgeText_Overdue_ShowsLabel()
    {
        Assert.Equal(WorkOrderOverdueDisplay.OverdueLabel, WorkOrderOverdueDisplay.GetBadgeText(true));
        Assert.Equal("延期", WorkOrderOverdueDisplay.GetBadgeText(true));
    }

    [Fact]
    public void GetBadgeText_NotOverdue_ReturnsNull()
    {
        Assert.Null(WorkOrderOverdueDisplay.GetBadgeText(false));
    }

    [Fact]
    public void GetRowCssClass_Overdue_AppliesMarkerClass()
    {
        Assert.Equal(WorkOrderOverdueDisplay.OverdueCssClass, WorkOrderOverdueDisplay.GetRowCssClass(true));
        Assert.Equal(string.Empty, WorkOrderOverdueDisplay.GetRowCssClass(false));
    }

    [Fact]
    public void FakeRuleHint_MentionsTemporaryOverdueRule()
    {
        Assert.Contains("Fake", WorkOrderOverdueDisplay.FakeRuleHint);
        Assert.Contains("PlannedFinishUtc", WorkOrderOverdueDisplay.FakeRuleHint);
        Assert.Contains("待现场确认", WorkOrderOverdueDisplay.FakeRuleHint);
    }
}

public class WorkOrderCompletionRateDisplayTests
{
    [Fact]
    public void CompletionRate_Null_WhenPlanIsZero_ShowsEmDash()
    {
        // API：PlannedQuantity = 0 → CompletionRate = null；页面用同一 RatioDisplayFormatter。
        decimal? completionRateWhenPlanIsZero = null;
        Assert.Equal("—", RatioDisplayFormatter.FormatPercent(completionRateWhenPlanIsZero));
        Assert.DoesNotContain("0%", RatioDisplayFormatter.FormatPercent(completionRateWhenPlanIsZero));
    }

    [Fact]
    public void CompletionRate_Value_ShowsPercent_WithoutRecalculation()
    {
        Assert.Equal("33.33%", RatioDisplayFormatter.FormatPercent(0.3333333333333333m));
        Assert.Equal("100%", RatioDisplayFormatter.FormatPercent(1m));
    }
}
