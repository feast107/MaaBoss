using System;
using System.IO;
using System.Threading.Tasks;
using MaaBoss.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace MaaBoss.Core.Tests;

/// <summary>
/// CDP 集成测试：通过 PEB 注入启动 BOSS直聘 并连接其 V8 引擎。
/// 前置条件：BOSS直聘 未运行（或测试会自动结束现有进程）。
/// </summary>
public class CdpIntegrationTests : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly CdpService _cdp;
    private System.Diagnostics.Process? _process;

    private static string BossExePath => @"D:\Tool\Boss\boss-zhipin\boss-zhipin.exe";

    public CdpIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _cdp = new CdpService();
    }

    public async ValueTask DisposeAsync()
    {
        await _cdp.DisposeAsync();
        try { _process?.Kill(); } catch { /* ignored */ }
    }

    [Fact()]
    public async Task LaunchViaPebInjection_And_Connect_Cdp()
    {
        var exePath = BossExePath;
        Assert.True(File.Exists(exePath), $"找不到 BOSS直聘: {exePath}");

        // STEP 1: 通过 PEB 注入启动，注入 --remote-debugging-port=9222
        _output.WriteLine("[STEP 1] 通过 PEB 注入启动 BOSS直聘...");
        _process = CdpService.LaunchBoss(exePath, port: 9222);
        _output.WriteLine($"[STEP 1] 进程已启动 PID={_process.Id}，等待界面加载...");
        await Task.Delay(TimeSpan.FromSeconds(5));

        // STEP 2: 连接 CDP
        _output.WriteLine("[STEP 2] 连接 Chrome DevTools Protocol...");
        await _cdp.ConnectAsync(port: 9222);
        _output.WriteLine("[STEP 2] CDP 连接成功");

        // STEP 3: 执行 JS 获取页面信息
        _output.WriteLine("[STEP 3] 执行 JS 获取 document.title...");
        var titleResult = await _cdp.EvaluateAsync("document.title");
        _output.WriteLine($"[STEP 3] document.title = {titleResult}");

        // STEP 4: CDP 截屏
        _output.WriteLine("[STEP 4] CDP 截屏...");
        var b64 = await _cdp.ScreenshotAsync();
        var screenshotPath = Path.Combine(AppContext.BaseDirectory, "test_screenshots", $"cdp_screenshot_{DateTime.Now:HHmmss}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
        await File.WriteAllBytesAsync(screenshotPath, Convert.FromBase64String(b64));
        _output.WriteLine($"[STEP 4] 截图已保存: {screenshotPath}");

        // STEP 5: 通过 JS 点击"消息"按钮（比图像识别更可靠）
        _output.WriteLine("[STEP 5] 通过 JS 点击'消息'...");
        await _cdp.ClickByTextAsync("消息");
        _output.WriteLine("[STEP 5] 点击完成");

        // 等待页面切换
        await Task.Delay(TimeSpan.FromSeconds(2));
        var afterClickTitle = await _cdp.EvaluateAsync("document.title");
        _output.WriteLine($"[STEP 5] 点击后 title = {afterClickTitle}");
    }
}
