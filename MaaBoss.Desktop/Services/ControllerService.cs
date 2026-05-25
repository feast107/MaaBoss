using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MaaBoss.Desktop.Models;
using Newtonsoft.Json;

namespace MaaBoss.Desktop.Services;

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

    /// <summary>
    /// 连接到目标客户端。
    /// Win32 模式下会先查找/启动目标进程。
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
            DisconnectCore();

            var userPath = Path.Combine(AppContext.BaseDirectory, ".cache");
            Directory.CreateDirectory(userPath);
            MaaToolkit.Shared.Config.InitOption(userPath);

            MaaController controller;

            if (platform.ToLowerInvariant() == "adb")
            {
                var devices = MaaToolkit.Shared.AdbDevice.Find();
                if (devices.IsEmpty)
                    return new ConnectResult(false, "adb", "-", "未找到 ADB 设备");

                var device = devices[0];
                controller = device.ToAdbControllerWith(
                    link: LinkOption.Start,
                    check: CheckStatusOption.ThrowIfNotSucceeded);
            }
            else
            {
                var windows = MaaToolkit.Shared.Desktop.Window.Find();
                if (windows.IsEmpty)
                    return new ConnectResult(false, "win32", "-", "未找到目标窗口");

                DesktopWindowInfo? target = null;
                var searchName = windowName ?? "boss";
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

                controller = target.ToWin32Controller(
                    link: LinkOption.Start,
                    check: CheckStatusOption.ThrowIfNotSucceeded);
            }

            var resourcePath = Path.Combine(AppContext.BaseDirectory, "assets", "pipeline");
            var resource = new MaaResource(
                CheckStatusOption.ThrowIfNotSucceeded,
                new[] { resourcePath });

            _tasker = new MaaTasker
            {
                Controller = controller,
                Resource = resource,
                DisposeOptions = DisposeOptions.All,
            };

            if (!_tasker.IsInitialized)
                return new ConnectResult(false, platform, "-", "MaaTasker 初始化失败");

            // 获取分辨率
            var scJob = controller.Screencap();
            scJob.Wait().ThrowIfNot(MaaJobStatus.Succeeded);
            _tasker.Controller.GetResolution(out var rw, out var rh);

            return new ConnectResult(true, platform, $"{rw}x{rh}");
        }
        catch (Exception ex)
        {
            DisconnectCore();
            return new ConnectResult(false, platform, "-", ex.Message);
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
            var resourcePath = Path.Combine(AppContext.BaseDirectory, "assets", "pipeline");
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
