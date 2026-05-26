using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MaaBoss.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace MaaBoss.Core.Tests;

/// <summary>
/// 集成测试：使用 MaaFramework TemplateMatch 识别截图中的目标图片并自动点击。
/// 前置条件：BOSS直聘客户端必须已启动并处于前台。
/// </summary>
public class TemplateMatchClickTests : IDisposable
{
    private readonly ControllerService _controller;
    private readonly ITestOutputHelper _output;

    public TemplateMatchClickTests(ITestOutputHelper output)
    {
        _output = output;
        _controller = new ControllerService();
    }

    public void Dispose()
    {
        try { _controller.DisconnectAsync().Wait(TimeSpan.FromSeconds(3)); } catch { /* ignored */ }
    }

    [Fact]
    public async Task Connect_Screenshot_And_FindClick_MessagePng()
    {
        // ---- 1. 连接 Win32 ----
        _output.WriteLine("[STEP 1] 正在连接 Win32 客户端...");
        var connectResult = await _controller.ConnectAsync();
        Assert.True(connectResult.Success,
            $"连接失败: {connectResult.ErrorMessage}。请确保 BOSS直聘 客户端已启动。");
        _output.WriteLine($"[STEP 1] 连接成功，分辨率: {connectResult.Resolution}");

        // ---- 2. 截图保存（用于调试和验证） ----
        _output.WriteLine("[STEP 2] 正在截图...");
        var screenshotDir = Path.Combine(AppContext.BaseDirectory, "test_screenshots");
        Directory.CreateDirectory(screenshotDir);
        var screenshotPath = Path.Combine(screenshotDir, $"before_click_{DateTime.Now:HHmmss}.png");
        var scResult = await _controller.ScreenshotAsync(screenshotPath);
        Assert.True(scResult.Success, "截图失败");
        _output.WriteLine($"[STEP 2] 截图已保存: {screenshotPath}");

        // ---- 3. 验证模板图片资源存在 ----
        var templatePath = Path.Combine(AppContext.BaseDirectory, "assets", "image", "消息.png");
        Assert.True(File.Exists(templatePath), $"模板图片不存在: {templatePath}");
        _output.WriteLine($"[STEP 3] 模板图片存在: {templatePath}");

        // ---- 4. 使用 Pipeline TemplateMatch 识别并点击 ----
        _output.WriteLine("[STEP 4] 执行 TemplateMatch + Click Pipeline...");
        var pipelineOverride = new Dictionary<string, object>
        {
            ["ClickMessage"] = new Dictionary<string, object>
            {
                ["recognition"] = "FeatureMatch",
                ["template"] = "消息.png",
                ["detector"] = "SIFT",
                ["count"] = 4,
                ["ratio"] = 0.8,
                ["action"] = "Click",
                ["next"] = new List<string>()
            }
        };

        var result = await _controller.RunPipelineAsync("ClickMessage", pipelineOverride);
        Assert.True(result.Success, $"Pipeline 执行失败");
        _output.WriteLine($"[STEP 4] Pipeline 执行成功，命中节点: {result.NodeHit}");

        // ---- 5. 点击后再次截图，验证状态变化 ----
        var afterClickPath = Path.Combine(screenshotDir, $"after_click_{DateTime.Now:HHmmss}.png");
        var scAfter = await _controller.ScreenshotAsync(afterClickPath);
        Assert.True(scAfter.Success, "点击后截图失败");
        _output.WriteLine($"[STEP 5] 点击后截图已保存: {afterClickPath}");
    }
}
