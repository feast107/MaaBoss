using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace MaaBoss.Desktop.Infrastructure.Maa;

/// <summary>
/// MaaFramework Mock 控制器，用于框架验证和 UI 调试。
/// 接入真实 MaaFramework.Binding.CSharp 后，替换此类即可。
/// </summary>
public class MaaControllerMock : IMaaController
{
    private bool _connected;
    private (int, int) _resolution = (1920, 1080);
    private string? _windowName;

    public bool IsConnected => _connected;
    public (int Width, int Height) Resolution => _resolution;

    public async Task<ConnectResult> ConnectAsync(ControllerType type, string? adbAddress = null, string? windowName = null)
    {
        _windowName = windowName;
        await Task.Delay(500);

        // TODO: 真实实现中，Win32 模式下需要：
        // 1. 通过 Process.GetProcessesByName 查找 boss-zhipin.exe
        // 2. 若未运行，尝试启动进程
        // 3. 通过 FindWindow / EnumWindows 定位窗口句柄
        // 4. 使用 MaaFramework Win32 Controller API 连接

        _connected = true;
        _resolution = (1920, 1080);
        return new ConnectResult(true, type.ToString(), "1920x1080");
    }

    public async Task DisconnectAsync()
    {
        await Task.Delay(100);
        _connected = false;
    }

    public async Task<PipelineResult> RunPipelineAsync(string taskName, object? pipelineOverride = null)
    {
        await Task.Delay(300);
        return new PipelineResult(true, taskName, new { });
    }

    public async Task<RecogResult> RunRecognitionAsync(string imagePath, string recognitionType, int[]? roi = null)
    {
        await Task.Delay(200);
        return new RecogResult(true, new[] { 100, 200, 300, 400 }, 0.95);
    }

    public async Task<ScreenshotResult> ScreenshotAsync(string? savePath = null)
    {
        await Task.Delay(200);
        return new ScreenshotResult(true, savePath);
    }

    public async Task<ClickResult> ClickAsync(int x, int y)
    {
        await Task.Delay(50);
        return new ClickResult(true, x, y);
    }

    public async Task<SwipeResult> SwipeAsync(int x1, int y1, int x2, int y2, int durationMs = 500)
    {
        await Task.Delay(durationMs);
        return new SwipeResult(true, x1, y1, x2, y2);
    }

    public async Task<InputResult> InputTextAsync(string text)
    {
        await Task.Delay(100);
        return new InputResult(true, "input", text);
    }

    public async Task<InputResult> PressKeyAsync(int keycode)
    {
        await Task.Delay(50);
        return new InputResult(true, "key", keycode);
    }

    public async Task ReloadResourcesAsync()
    {
        await Task.Delay(100);
    }

    public void Dispose()
    {
        _ = DisconnectAsync();
    }
}
