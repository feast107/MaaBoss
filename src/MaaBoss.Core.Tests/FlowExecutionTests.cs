using System.Threading;
using System.Threading.Tasks;
using MaaBoss.Core.Models;
using MaaBoss.Core.Services;
using Xunit;

namespace MaaBoss.Core.Tests;

/// <summary>
/// 基于 MaaBoss.Core 的流程执行单元测试。
/// 可单独运行以验证 Pipeline 生成和执行逻辑，无需启动 Avalonia UI。
/// </summary>
public class FlowExecutionTests
{
    [Fact]
    public void FlowStep_DefaultValues_AreCorrect()
    {
        var step = new FlowStep();

        Assert.Equal(FlowActionType.Click, step.Action);
        Assert.Equal(FlowRecognitionType.DirectHit, step.Recognition);
        Assert.Equal("新步骤", step.Name);
        Assert.Equal(0.8, step.Threshold);
        Assert.Equal(20000, step.Timeout);
        Assert.Equal(200, step.PreDelay);
        Assert.Equal(500, step.PostDelay);
    }

    [Fact]
    public void FlowStep_Recognition_CanBeChanged()
    {
        var step = new FlowStep
        {
            Recognition = FlowRecognitionType.TemplateMatch,
            Template = "test.png",
            Threshold = 0.9,
            RoiX = 10, RoiY = 20, RoiW = 100, RoiH = 50
        };

        Assert.Equal(FlowRecognitionType.TemplateMatch, step.Recognition);
        Assert.Equal("test.png", step.Template);
        Assert.Equal(0.9, step.Threshold);
        Assert.Equal(100, step.RoiW);
    }

    [Fact]
    public void FlowRecognitionType_HasAllTenValues()
    {
        var values = System.Enum.GetValues<FlowRecognitionType>();

        Assert.Equal(10, values.Length);
        Assert.Contains(FlowRecognitionType.DirectHit, values);
        Assert.Contains(FlowRecognitionType.TemplateMatch, values);
        Assert.Contains(FlowRecognitionType.FeatureMatch, values);
        Assert.Contains(FlowRecognitionType.ColorMatch, values);
        Assert.Contains(FlowRecognitionType.OCR, values);
        Assert.Contains(FlowRecognitionType.NeuralNetworkClassify, values);
        Assert.Contains(FlowRecognitionType.NeuralNetworkDetect, values);
        Assert.Contains(FlowRecognitionType.And, values);
        Assert.Contains(FlowRecognitionType.Or, values);
        Assert.Contains(FlowRecognitionType.Custom, values);
    }

    [Fact]
    public void ServiceLocator_RegisterAndGet_Works()
    {
        ServiceLocator.Reset();
        var logService = new LogService();

        ServiceLocator.Register(logService);
        var retrieved = ServiceLocator.Get<LogService>();

        Assert.Same(logService, retrieved);
    }

    [Fact]
    public void ServiceLocator_GetOrCreate_CreatesNewInstance()
    {
        ServiceLocator.Reset();

        var instance1 = ServiceLocator.GetOrCreate<LogService>();
        var instance2 = ServiceLocator.GetOrCreate<LogService>();

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void LogService_AppendsAndClears()
    {
        var log = new LogService();
        log.Info("test message");

        Assert.Contains("test message", log.LogText);
        Assert.Contains("INFO", log.LogText);

        log.Clear();
        Assert.Equal("", log.LogText);
    }

    [Fact]
    public void ToolResult_Ok_ReturnsSuccess()
    {
        var result = ToolResult.Ok(("key", "value"));

        Assert.True(result.Success);
        Assert.NotNull(result.Extra);
        Assert.Equal("value", result.Extra!["key"]);
    }

    [Fact]
    public void ToolResult_Err_ReturnsFailure()
    {
        var result = ToolResult.Err("CODE", "message", "suggestion");

        Assert.False(result.Success);
        Assert.Equal("CODE", result.ErrorCode);
        Assert.Equal("message", result.ErrorMessage);
        Assert.Equal("suggestion", result.Suggestion);
    }
}
