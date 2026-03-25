using Gov.Common;
using Gov.Protocol;
using FluentAssertions;
using Xunit;

namespace Gov.Tests;

public class FailureClassifierTests
{
    private static ClassificationInput CreateInput(
        int exitCode = 1,
        int durationMs = 30000,
        double commitRatioAtExit = 0.5,
        double peakCommitRatio = 0.5,
        double peakProcessCommitGb = 0.5,
        bool stderrHadDiagnostics = true,
        double commitChargeGb = 16.0,
        double commitLimitGb = 32.0,
        int recommendedParallelism = 4) => new()
    {
        ExitCode = exitCode,
        DurationMs = durationMs,
        CommitRatioAtExit = commitRatioAtExit,
        PeakCommitRatioDuringExecution = peakCommitRatio,
        PeakProcessCommitGb = peakProcessCommitGb,
        StderrHadDiagnostics = stderrHadDiagnostics,
        CommitChargeGb = commitChargeGb,
        CommitLimitGb = commitLimitGb,
        RecommendedParallelism = recommendedParallelism,
    };

    [Fact]
    public void Classify_ExitCodeZero_ReturnsSuccess()
    {
        var result = FailureClassifier.Classify(CreateInput(exitCode: 0));

        result.Classification.Should().Be(FailureClassification.Success);
        result.ShouldRetry.Should().BeFalse();
        result.Confidence.Should().Be(1.0);
        result.Message.Should().BeNull();
    }

    [Fact]
    public void Classify_NormalCompileError_WithDiagnostics_ReturnsNormalCompileError()
    {
        var result = FailureClassifier.Classify(CreateInput(
            exitCode: 2,
            commitRatioAtExit: 0.5,
            peakCommitRatio: 0.5,
            peakProcessCommitGb: 0.3,
            stderrHadDiagnostics: true));

        result.Classification.Should().Be(FailureClassification.NormalCompileError);
        result.ShouldRetry.Should().BeFalse();
        result.Message.Should().BeNull();
    }

    [Fact]
    public void Classify_HighCommitRatio_LikelyOOM()
    {
        var result = FailureClassifier.Classify(CreateInput(
            exitCode: 1,
            commitRatioAtExit: 0.95,
            peakCommitRatio: 0.97,
            peakProcessCommitGb: 3.0,
            stderrHadDiagnostics: false));

        result.Classification.Should().Be(FailureClassification.LikelyOOM);
        result.ShouldRetry.Should().BeTrue();
        result.Message.Should().Contain("Memory Pressure");
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.6);
    }

    [Fact]
    public void Classify_ModerateCommitRatio_LikelyPagingDeath()
    {
        // Evidence: commitRatio >= 0.88 (0.25) + peakCommitRatio >= 0.95 (0.3) = 0.55
        // With no diagnostics (+0.2) = 0.75 → LikelyOOM
        // So use diagnostics=true to reduce: 0.25 + 0.3 = 0.55 → LikelyPagingDeath
        var result = FailureClassifier.Classify(CreateInput(
            exitCode: 1,
            commitRatioAtExit: 0.90,
            peakCommitRatio: 0.96,
            peakProcessCommitGb: 1.0,
            stderrHadDiagnostics: true));

        result.Classification.Should().Be(FailureClassification.LikelyPagingDeath);
        result.ShouldRetry.Should().BeTrue();
        result.Message.Should().Contain("Paging Pressure");
    }

    [Fact]
    public void Classify_LowEvidence_NoDiagnostics_ReturnsUnknown()
    {
        var result = FailureClassifier.Classify(CreateInput(
            exitCode: 1,
            commitRatioAtExit: 0.5,
            peakCommitRatio: 0.5,
            peakProcessCommitGb: 0.3,
            stderrHadDiagnostics: false,
            durationMs: 60000));

        result.Classification.Should().Be(FailureClassification.Unknown);
        result.ShouldRetry.Should().BeFalse();
        result.Message.Should().Contain("Unable to determine cause");
    }

    [Fact]
    public void Classify_ShortDurationHighMemory_AddsEvidence()
    {
        // Short duration (<5000ms) + high memory (>=1.5GB) adds 0.15
        // Combined with no diagnostics (0.2) + commitRatio>=0.88 (0.25) = 0.60 → LikelyOOM
        var result = FailureClassifier.Classify(CreateInput(
            exitCode: 1,
            durationMs: 3000,
            commitRatioAtExit: 0.90,
            peakCommitRatio: 0.5,
            peakProcessCommitGb: 2.0,
            stderrHadDiagnostics: false));

        result.Classification.Should().Be(FailureClassification.LikelyOOM);
        result.Reasons.Should().Contain(r => r.Contains("short duration"));
    }

    [Fact]
    public void Classify_HighPeakProcess_AddsEvidence()
    {
        var result = FailureClassifier.Classify(CreateInput(
            exitCode: 1,
            peakProcessCommitGb: 3.0,
            stderrHadDiagnostics: true));

        result.Reasons.Should().Contain(r => r.Contains("peaked at"));
    }

    [Fact]
    public void Classify_VeryHighCommitRatioAtExit_Adds04Evidence()
    {
        // commitRatioAtExit >= 0.92 adds 0.4
        var result = FailureClassifier.Classify(CreateInput(
            exitCode: 1,
            commitRatioAtExit: 0.94,
            stderrHadDiagnostics: true,
            peakProcessCommitGb: 0.1));

        result.Reasons.Should().Contain(r => r.Contains("commit ratio") && r.Contains("at exit"));
    }

    [Fact]
    public void Classify_ConfidenceCappedAtOne()
    {
        // Max out all evidence signals
        var result = FailureClassifier.Classify(CreateInput(
            exitCode: 1,
            commitRatioAtExit: 0.95,
            peakCommitRatio: 0.98,
            peakProcessCommitGb: 4.0,
            stderrHadDiagnostics: false,
            durationMs: 2000));

        result.Confidence.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void Classify_OomMessage_ContainsParallelismRecommendation()
    {
        var result = FailureClassifier.Classify(CreateInput(
            exitCode: 1,
            commitRatioAtExit: 0.95,
            peakCommitRatio: 0.97,
            peakProcessCommitGb: 3.0,
            stderrHadDiagnostics: false,
            recommendedParallelism: 2));

        result.Message.Should().Contain("CMAKE_BUILD_PARALLEL_LEVEL=2");
        result.Message.Should().Contain("/m:2");
        result.Message.Should().Contain("-j2");
    }

    [Fact]
    public void Classify_ReasonsListPopulated_ForOom()
    {
        var result = FailureClassifier.Classify(CreateInput(
            exitCode: 1,
            commitRatioAtExit: 0.95,
            peakCommitRatio: 0.97,
            peakProcessCommitGb: 3.0,
            stderrHadDiagnostics: false));

        result.Reasons.Should().NotBeNullOrEmpty();
        result.Reasons!.Count.Should().BeGreaterThanOrEqualTo(3);
    }
}
