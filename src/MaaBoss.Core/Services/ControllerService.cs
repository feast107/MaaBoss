using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MaaBoss.Core.Models;
using Newtonsoft.Json;

namespace MaaBoss.Core.Services;

/// <summary>
/// 控制器服务：管理 MaaFramework 连接和原子操作。
/// </summary>
public class ControllerService
{
    private MaaTasker? _tasker;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsConnected => _tasker?.Controller?.IsConnected ?? false;
    public (int W, int H) Resolution
    {
        get
        {
            if (_tasker?.Controller?.GetResolution(out var w, out var h) == true)
                return (w, h);
            return (0, 0);
        }
    }

    /// <summary>目标窗口句柄（Win32 模式下有效）</summary>
    public IntPtr TargetHwnd { get; private set; } = IntPtr.Zero;

    /// <summary>当前鼠标输入方式</summary>
    public Win32InputMethod CurrentMouseMethod { get; private set; } = Win32InputMethod.SendMessageWithCursorPos;

    /// <summary>当前截图方式</summary>
    public Win32ScreencapMethod CurrentScreencapMethod { get; private set; } = Win32ScreencapMethod.DXGI_DesktopDup_Window;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private const uint WM_MOUSEWHEEL = 0x020A;
    private static IntPtr MAKEWPARAM(ushort low, ushort high) => (IntPtr)(((uint)high << 16) | low);
    private static IntPtr MAKELPARAM(ushort low, ushort high) => (IntPtr)(((uint)high << 16) | low);

    /// <summary>
    /// 获取目标窗口在屏幕上的完整矩形（含边框、标题栏）。
    /// </summary>
    public (int X, int Y, int W, int H) GetWindowRect()
    {
        if (TargetHwnd == IntPtr.Zero || !GetWindowRect(TargetHwnd, out var rect))
            return (0, 0, 0, 0);
        return (rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    /// <summary>
    /// 获取目标窗口客户区在屏幕上的位置和尺寸。
    /// </summary>
    public (int X, int Y, int W, int H) GetClientRectOnScreen()
    {
        if (TargetHwnd == IntPtr.Zero) return (0, 0, 0, 0);
        var pt = new POINT { X = 0, Y = 0 };
        if (!GetClientRect(TargetHwnd, out var clientRect) || !ClientToScreen(TargetHwnd, ref pt))
            return (0, 0, 0, 0);
        return (pt.X, pt.Y, clientRect.Right - clientRect.Left, clientRect.Bottom - clientRect.Top);
    }

    /// <summary>
    /// 连接到目标客户端。
    /// Win32 模式下会先查找/启动目标进程。
    /// </summary>
    public async Task<ConnectResult> ConnectAsync(
        string? windowName = null,
        Win32ScreencapMethod screencapMethod = Win32ScreencapMethod.PrintWindow,
        Win32InputMethod mouseMethod = Win32InputMethod.SendMessageWithCursorPos,
        Win32InputMethod keyboardMethod = Win32InputMethod.PostMessage,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            DisconnectCore();

            var userPath = Path.Combine(AppContext.BaseDirectory, ".cache");
            Directory.CreateDirectory(userPath);
            // MaaFramework 会尝试加载 runtimes/win-x64/native/plugins 下的 DLL，
            // 目录不存在时会报 ERROR，提前创建空目录避免日志噪音。
            var pluginDir = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "plugins");
            Directory.CreateDirectory(pluginDir);
            MaaToolkit.Shared.Config.InitOption(userPath);

            var windows = MaaToolkit.Shared.Desktop.Window.Find();
            if (windows.IsEmpty)
                return new ConnectResult(false, "win32", "-", "未找到目标窗口");

            DesktopWindowInfo? target = null;
            var searchName = windowName ?? "BOSS直聘";
            foreach (var win in windows)
            {
                if (win.Name.Contains(searchName, StringComparison.OrdinalIgnoreCase) ||
                    win.ClassName.Contains(searchName, StringComparison.OrdinalIgnoreCase))
                {
                    target = win;
                    break;
                }
            }
            target ??= windows[0];

            TargetHwnd = target.Handle;
            CurrentMouseMethod = mouseMethod;
            CurrentScreencapMethod = screencapMethod;

            var controller = target.ToWin32ControllerWith(
                screencapMethod: screencapMethod,
                mouseMethod: mouseMethod,
                keyboardMethod: keyboardMethod,
                link: LinkOption.Start,
                check: CheckStatusOption.ThrowIfNotSucceeded);

            var resourcePath = Path.Combine(AppContext.BaseDirectory, "assets");
            var resource = new MaaResource(CheckStatusOption.ThrowIfNotSucceeded, resourcePath);

            _tasker = new MaaTasker
            {
                Controller = controller,
                Resource = resource,
                DisposeOptions = DisposeOptions.All,
            };

            if (!_tasker.IsInitialized)
                return new ConnectResult(false, "win32", "-", "MaaTasker 初始化失败");

            // 获取分辨率
            var scJob = controller.Screencap();
            scJob.Wait().ThrowIfNot(MaaJobStatus.Succeeded);
            _tasker.Controller.GetResolution(out var rw, out var rh);

            return new ConnectResult(true, "win32", $"{rw}x{rh}");
        }
        catch (Exception ex)
        {
            DisconnectCore();
            return new ConnectResult(false, "win32", "-", ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            DisconnectCore();
        }
        finally
        {
            _lock.Release();
        }
    }

    private void DisconnectCore()
    {
        _tasker?.Dispose();
        _tasker = null;
        TargetHwnd = IntPtr.Zero;
        CurrentMouseMethod = Win32InputMethod.SendMessageWithCursorPos;
        CurrentScreencapMethod = Win32ScreencapMethod.DXGI_DesktopDup_Window;
    }

    public async Task<ScreenshotResult> ScreenshotAsync(string? savePath, CancellationToken ct = default)
    {
        EnsureConnected();
        return await Task.Run(() =>
        {
            var job = _tasker!.Controller.Screencap();
            job.Wait().ThrowIfNot(MaaJobStatus.Succeeded);

            using var buffer = new MaaImageBuffer();
            if (!_tasker.Controller.GetCachedImage(buffer))
                throw new InvalidOperationException("获取截图失败");

            using var image = MaaImage.Load(buffer);
            if (!string.IsNullOrEmpty(savePath))
            {
                var dir = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                image.Save(savePath);
            }

            return new ScreenshotResult(true, savePath);
        }, ct);
    }

    public async Task<ClickResult> ClickAsync(int x, int y, CancellationToken ct = default)
    {
        EnsureConnected();
        return await Task.Run(() =>
        {
            var job = _tasker!.Controller.Click(x, y);
            job.Wait().ThrowIfNot(MaaJobStatus.Succeeded);
            return new ClickResult(true, x, y);
        }, ct);
    }

    public async Task<SwipeResult> SwipeAsync(int x1, int y1, int x2, int y2, int durationMs = 500, CancellationToken ct = default)
    {
        EnsureConnected();
        return await Task.Run(() =>
        {
            var job = _tasker!.Controller.Swipe(x1, y1, x2, y2, durationMs);
            job.Wait().ThrowIfNot(MaaJobStatus.Succeeded);
            return new SwipeResult(true, x1, y1, x2, y2);
        }, ct);
    }

    public async Task<ScrollResult> ScrollAsync(int x, int y, int delta, CancellationToken ct = default)
    {
        EnsureConnected();
        return await Task.Run(() =>
        {
            if (TargetHwnd != IntPtr.Zero)
            {
                // 使用 SendMessage 发送 WM_MOUSEWHEEL，delta 为滚轮增量（正数向上，负数向下）
                var wParam = MAKEWPARAM(0, (ushort)delta);
                var lParam = MAKELPARAM((ushort)x, (ushort)y);
                SendMessage(TargetHwnd, WM_MOUSEWHEEL, wParam, lParam);
            }
            return new ScrollResult(true, x, y, delta);
        }, ct);
    }

    public async Task<PipelineResult> RunPipelineAsync(string taskName, object? param = null, CancellationToken ct = default)
    {
        EnsureConnected();
        return await Task.Run(() =>
        {
            string pipelineOverride = "{}";
            if (param != null)
                pipelineOverride = JsonConvert.SerializeObject(param);

            var job = _tasker!.AppendTask(taskName, pipelineOverride);
            job.Wait().ThrowIfNot(MaaJobStatus.Succeeded);

            var detail = job.QueryTaskDetail();
            var nodeHit = taskName;
            if (detail.NodeIdList.Count > 0)
            {
                var lastNodeId = detail.NodeIdList[^1];
                var node = NodeDetail.Query(lastNodeId, _tasker);
                nodeHit = node.NodeName ?? taskName;
            }

            return new PipelineResult(true, nodeHit, detail);
        }, ct);
    }

    public async Task ReloadResourcesAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        await Task.Run(() =>
        {
            var resourcePath = Path.Combine(AppContext.BaseDirectory, "assets");
            var newResource = new MaaResource(
                CheckStatusOption.ThrowIfNotSucceeded,
                new[] { resourcePath });
            _tasker!.Resource = newResource;
        }, ct);
    }

    private void EnsureConnected()
    {
        if (_tasker == null || !_tasker.Controller.IsConnected)
            throw new InvalidOperationException("控制器未连接，请先调用 launch_app");
    }
}
