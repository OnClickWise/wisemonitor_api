using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Helpers;
using WiseMonitor.Api.Models;
using Xunit;

namespace WiseMonitor.Api.Tests.Helpers;

public class KeyboardProductivityHelperTests
{
    [Fact]
    public void Calculate_NullMetrics_DoesNotThrowAndScoresZero()
    {
        var dto = new KeyboardEventCreateDTO { Metrics = null };

        var result = KeyboardProductivityHelper.Calculate(dto);

        Assert.Equal(0, result.Score);
        Assert.Equal(KeyboardClassification.Improdutivo, result.Classification);
    }

    [Fact]
    public void CalculateQuality_NullMetrics_DoesNotThrowAndReturnsZero()
    {
        var dto = new KeyboardEventCreateDTO { Metrics = null };

        var result = KeyboardProductivityHelper.CalculateQuality(dto, durationMinutes: 5);

        Assert.Equal(0, result.WordsPerMinute);
        Assert.Equal(0, result.CorrectionRate);
    }

    [Fact]
    public void CalculateQuality_ComputesWordsPerMinuteAndCorrectionRate()
    {
        var dto = new KeyboardEventCreateDTO
        {
            Metrics = new KeyboardMetricsDTO
            {
                Words = 100,
                TotalKeystrokes = 500,
                BackspaceCount = 50
            }
        };

        var result = KeyboardProductivityHelper.CalculateQuality(dto, durationMinutes: 10);

        Assert.Equal(10.0, result.WordsPerMinute);
        Assert.Equal(0.1, result.CorrectionRate);
    }

    [Fact]
    public void CalculateQuality_ZeroDurationOrKeystrokes_AvoidsDivideByZero()
    {
        var dto = new KeyboardEventCreateDTO
        {
            Metrics = new KeyboardMetricsDTO { Words = 10, TotalKeystrokes = 0, BackspaceCount = 0 }
        };

        var result = KeyboardProductivityHelper.CalculateQuality(dto, durationMinutes: 0);

        Assert.Equal(0, result.WordsPerMinute);
        Assert.Equal(0, result.CorrectionRate);
    }
}
