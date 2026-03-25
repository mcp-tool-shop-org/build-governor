using Gov.Common;
using FluentAssertions;
using Xunit;

namespace Gov.Tests;

public class TokenBudgetTests
{
    private static MemoryStatus CreateMemoryStatus(
        long commitChargeBytes = 16L * 1024 * 1024 * 1024,
        long commitLimitBytes = 32L * 1024 * 1024 * 1024,
        long totalPhysicalBytes = 32L * 1024 * 1024 * 1024,
        long availablePhysicalBytes = 16L * 1024 * 1024 * 1024,
        int memoryLoadPercent = 50)
    {
        var commitRatio = commitLimitBytes > 0 ? (double)commitChargeBytes / commitLimitBytes : 0;
        return new MemoryStatus
        {
            TotalPhysicalBytes = totalPhysicalBytes,
            AvailablePhysicalBytes = availablePhysicalBytes,
            CommitChargeBytes = commitChargeBytes,
            CommitLimitBytes = commitLimitBytes,
            CommitRatio = commitRatio,
            MemoryLoadPercent = memoryLoadPercent,
        };
    }

    [Fact]
    public void CalculateTokenBudget_NormalMemory_ReturnsNormalThrottle()
    {
        var status = CreateMemoryStatus(
            commitChargeBytes: 10L * 1024 * 1024 * 1024,
            commitLimitBytes: 32L * 1024 * 1024 * 1024);
        var config = new TokenBudgetConfig();

        var budget = WindowsMemoryMetrics.CalculateTokenBudget(status, config);

        budget.ThrottleLevel.Should().Be(ThrottleLevel.Normal);
        budget.TotalTokens.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void CalculateTokenBudget_HighCommitRatio_ReturnsCaution()
    {
        // 0.85 commit ratio → Caution (>= 0.80, < 0.88)
        var status = CreateMemoryStatus(
            commitChargeBytes: 27L * 1024 * 1024 * 1024,
            commitLimitBytes: 32L * 1024 * 1024 * 1024);
        var config = new TokenBudgetConfig();

        var budget = WindowsMemoryMetrics.CalculateTokenBudget(status, config);

        budget.ThrottleLevel.Should().Be(ThrottleLevel.Caution);
    }

    [Fact]
    public void CalculateTokenBudget_VeryHighCommitRatio_ReturnsSoftStop()
    {
        // 0.90 commit ratio → SoftStop (>= 0.88, < 0.92)
        var status = CreateMemoryStatus(
            commitChargeBytes: 29L * 1024 * 1024 * 1024,
            commitLimitBytes: 32L * 1024 * 1024 * 1024);
        var config = new TokenBudgetConfig();

        var budget = WindowsMemoryMetrics.CalculateTokenBudget(status, config);

        budget.ThrottleLevel.Should().Be(ThrottleLevel.SoftStop);
    }

    [Fact]
    public void CalculateTokenBudget_CriticalCommitRatio_ReturnsHardStop()
    {
        // 0.95 commit ratio → HardStop (>= 0.92)
        var status = CreateMemoryStatus(
            commitChargeBytes: 31L * 1024 * 1024 * 1024,
            commitLimitBytes: 32L * 1024 * 1024 * 1024);
        var config = new TokenBudgetConfig();

        var budget = WindowsMemoryMetrics.CalculateTokenBudget(status, config);

        budget.ThrottleLevel.Should().Be(ThrottleLevel.HardStop);
    }

    [Fact]
    public void CalculateTokenBudget_TokensClampedToMin()
    {
        // Very low available commit → tokens should be clamped to MinTokens (1)
        var status = CreateMemoryStatus(
            commitChargeBytes: 31L * 1024 * 1024 * 1024,
            commitLimitBytes: 32L * 1024 * 1024 * 1024);
        var config = new TokenBudgetConfig { MinTokens = 1, SafetyReserveGb = 8.0 };

        var budget = WindowsMemoryMetrics.CalculateTokenBudget(status, config);

        budget.TotalTokens.Should().BeGreaterThanOrEqualTo(config.MinTokens);
    }

    [Fact]
    public void CalculateTokenBudget_TokensClampedToMax()
    {
        // Huge available commit → tokens should be clamped to MaxTokens
        var status = CreateMemoryStatus(
            commitChargeBytes: 10L * 1024 * 1024 * 1024,
            commitLimitBytes: 256L * 1024 * 1024 * 1024);
        var config = new TokenBudgetConfig { MaxTokens = 32 };

        var budget = WindowsMemoryMetrics.CalculateTokenBudget(status, config);

        budget.TotalTokens.Should().BeLessThanOrEqualTo(config.MaxTokens);
    }

    [Fact]
    public void CalculateTokenBudget_RecommendedParallelism_AtLeastOne()
    {
        var status = CreateMemoryStatus(
            commitChargeBytes: 31L * 1024 * 1024 * 1024,
            commitLimitBytes: 32L * 1024 * 1024 * 1024);
        var config = new TokenBudgetConfig();

        var budget = WindowsMemoryMetrics.CalculateTokenBudget(status, config);

        budget.RecommendedParallelism.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void CalculateTokenBudget_CustomConfig_Respected()
    {
        var status = CreateMemoryStatus(
            commitChargeBytes: 10L * 1024 * 1024 * 1024,
            commitLimitBytes: 64L * 1024 * 1024 * 1024);
        var config = new TokenBudgetConfig
        {
            GbPerToken = 4.0,
            SafetyReserveGb = 4.0,
            MinTokens = 2,
            MaxTokens = 16,
        };

        var budget = WindowsMemoryMetrics.CalculateTokenBudget(status, config);

        budget.TotalTokens.Should().BeGreaterThanOrEqualTo(config.MinTokens);
        budget.TotalTokens.Should().BeLessThanOrEqualTo(config.MaxTokens);
    }

    [Fact]
    public void MemoryStatus_DerivedProperties_CalculateCorrectly()
    {
        var status = CreateMemoryStatus(
            totalPhysicalBytes: 32L * 1024 * 1024 * 1024,
            availablePhysicalBytes: 16L * 1024 * 1024 * 1024,
            commitChargeBytes: 20L * 1024 * 1024 * 1024,
            commitLimitBytes: 40L * 1024 * 1024 * 1024);

        status.TotalPhysicalGb.Should().BeApproximately(32.0, 0.01);
        status.AvailablePhysicalGb.Should().BeApproximately(16.0, 0.01);
        status.CommitChargeGb.Should().BeApproximately(20.0, 0.01);
        status.CommitLimitGb.Should().BeApproximately(40.0, 0.01);
    }
}
