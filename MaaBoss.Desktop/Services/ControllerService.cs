using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaaBoss.Desktop.Infrastructure.Maa;

namespace MaaBoss.Desktop.Services;

/// <summary>
/// 控制器服务：管理 MaaFramework 连接和原子操作。
/// </summary>
public class ControllerService
{
    private IMaaController? _controller;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsConnected => _controller?.IsConnected ?? false;
    public (int W, int H) Resolution => _controller?.Resolution ?? (0, 0);

    /// <summary>
    /// 连接到目标客户端。
    /// Win32 模式下会先查找/启动 boss-zhipin.exe 进程。
    /// </summary>
    public async Task<ConnectResult> ConnectAsync(
        string platform,
        string? adbAddress = null,
        string? windowName = null,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _controller?.Dispose();
            _controller = new MaaControllerMock(); // TODO: 替换为真实 MaaFramework.Binding.CSharp 实现

            var type = platform.ToLowerInvariant() == "adb" ? ControllerType.Adb : ControllerType.Win32;

            if (type == ControllerType.Win32)
            {
                // 默认使用 boss-zhipin.exe 作为进程名
                var targetWindow = string.IsNullOrWhiteSpace(windowName) ? "boss-zhipin.exe" : windowName;

                // TODO: 真实实现中，这里需要：
                // 1. 查找进程是否已运行
                // 2. 若未运行，启动进程
                // 3. 获取窗口句柄后连接

                // Mock 下仅记录日志
                Debug.WriteLine($"[Win32] 目标进程/窗口: {targetWindow}");
            }

            return await _controller.ConnectAsync(type, adbAddress, windowName);
        }
        finally { _lock.Release(); }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_controller != null)
            {
                await _controller.DisconnectAsync();
                _controller.Dispose();
                _controller = null;
            }
        }
        finally { _lock.Release(); }
    }

    public async Task<ScreenshotResult> ScreenshotAsync(string? savePath, CancellationToken ct = default)
    {
        EnsureConnected();
        return await _controller!.ScreenshotAsync(savePath);
    }

    public async Task<ClickResult> ClickAsync(int x, int y, CancellationToken ct = default)
    {
        EnsureConnected();
        return await _controller!.ClickAsync(x, y);
    }

    public async Task<SwipeResult> SwipeAsync(int x1, int y1, int x2, int y2, int durationMs = 500, CancellationToken ct = default)
    {
        EnsureConnected();
        return await _controller!.SwipeAsync(x1, y1, x2, y2, durationMs);
    }

    public async Task<PipelineResult> RunPipelineAsync(string taskName, object? param = null, CancellationToken ct = default)
    {
        EnsureConnected();
        return await _controller!.RunPipelineAsync(taskName, param);
    }

    public async Task ReloadResourcesAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        await _controller!.ReloadResourcesAsync();
    }

    private void EnsureConnected()
    {
        if (_controller == null || !_controller.IsConnected)
            throw new InvalidOperationException("控制器未连接，请先调用 launch_app");
    }
}
