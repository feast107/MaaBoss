using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MaaBoss.Core.Messaging;
using MaaBoss.Core.Models;
using MaaBoss.Core.Services;
using MaaBoss.Desktop.Infrastructure;
using Newtonsoft.Json.Linq;

namespace MaaBoss.Desktop.ViewModels;

public partial class DebugViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial Bitmap? Screenshot { get; set; }

    [ObservableProperty]
    public partial string PipelineName { get; set; } = "Startup";

    [ObservableProperty]
    public partial string LogText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial string CursorPosText { get; set; } = "鼠标在截图区域内移动以查看坐标";

    [ObservableProperty]
    public partial string DiagnosticText { get; set; } = "";

    // 日志折叠
    [ObservableProperty]
    public partial bool IsLogExpanded { get; set; } = true;

    // 流程执行
    [ObservableProperty]
    public partial ObservableCollection<FlowStep> Steps { get; set; } = new();

    [ObservableProperty]
    public partial bool IsFlowRunning { get; set; }

    // 已保存的流程列表
    [ObservableProperty]
    public partial ObservableCollection<string> SavedFlows { get; set; } = new();

    // 实时截图
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveButtonText))]
    public partial bool IsLiveCapturing { get; set; }

    public string LiveButtonText => IsLiveCapturing ? "■ 停止" : "● 实时";

    private readonly ControllerService _controller;
    private readonly LogService _log;
    private CancellationTokenSource? _flowCts;
    private CancellationTokenSource? _liveCaptureCts;

    public DebugViewModel()
    {
        _controller = ServiceLocator.Get<ControllerService>();
        _log = ServiceLocator.Get<LogService>();

        WeakReferenceMessenger.Default.Register<ConnectionStateChangedMessage>(this, (_, msg) =>
        {
            IsConnected = msg.Value?.Success ?? false;
        });

        WeakReferenceMessenger.Default.Register<LogMessage>(this, (_, msg) =>
        {
            LogText = msg.Value;
        });
    }

    public (int W, int H) GetControllerResolution() => _controller.Resolution;

    public void UpdateDiagnostic(Bitmap? bitmap)
    {
        if (bitmap == null)
        {
            DiagnosticText = "";
            return;
        }

        var srcSize = bitmap.PixelSize;
        var (ctrlW, ctrlH) = _controller.Resolution;
        var hwnd = _controller.TargetHwnd;
        var (winX, winY, winW, winH) = _controller.GetWindowRect();
        var (cliX, cliY, cliW, cliH) = _controller.GetClientRectOnScreen();
        var mouse = _controller.CurrentMouseMethod;
        var scap = _controller.CurrentScreencapMethod;

        DiagnosticText = $"截图: {srcSize.Width}x{srcSize.Height} | 控制器: {ctrlW}x{ctrlH} | 鼠标: {mouse} | 截图方式: {scap}\n" +
                         $"HWND: {hwnd} | 窗口: ({winX},{winY}) {winW}x{winH} | 客户区: ({cliX},{cliY}) {cliW}x{cliH}";
    }

    public void UpdateCursorPosition(int imgX, int imgY, int targetX, int targetY, double pctX = 0, double pctY = 0)
    {
        if (imgX < 0)
            CursorPosText = "鼠标在截图区域内移动以查看坐标";
        else
            CursorPosText = $"截图内: ({imgX}, {imgY})  [控件: {pctX:P1}, {pctY:P1}]  →  目标: ({targetX}, {targetY})";
    }

    #region Screenshot & Debug

    private static readonly string LiveBufferPath = Path.Combine(AppContext.BaseDirectory, "screenshots", "live_buffer.png");

    [RelayCommand]
    private async Task TakeScreenshotAsync()
    {
        IsBusy = true;
        _log.Info("开始截图...");
        try
        {
            var bufferDir = Path.GetDirectoryName(LiveBufferPath)!;
            if (!Directory.Exists(bufferDir))
                Directory.CreateDirectory(bufferDir);

            var result = await _controller.ScreenshotAsync(LiveBufferPath);
            if (result.Success && File.Exists(LiveBufferPath))
            {
                var bytes = File.ReadAllBytes(LiveBufferPath);
                using var ms = new MemoryStream(bytes);
                Screenshot = new Bitmap(ms);
                UpdateDiagnostic(Screenshot);
                _log.Info($"截图已刷新: {LiveBufferPath}");
            }
            else
            {
                _log.Warn("截图失败或控制器未连接");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"截图异常: {ex.Message}");
        }
        IsBusy = false;
    }

    [RelayCommand]
    private async Task ToggleLiveCaptureAsync()
    {
        if (IsLiveCapturing)
        {
            _liveCaptureCts?.Cancel();
            IsLiveCapturing = false;
            _log.Info("实时截图已停止");
            return;
        }

        IsLiveCapturing = true;
        _liveCaptureCts = new CancellationTokenSource();
        var ct = _liveCaptureCts.Token;
        _log.Info("实时截图已启动 (间隔 500ms，缓冲文件: live_buffer.png)");

        var bufferDir = Path.GetDirectoryName(LiveBufferPath)!;
        if (!Directory.Exists(bufferDir))
            Directory.CreateDirectory(bufferDir);

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await _controller.ScreenshotAsync(LiveBufferPath, ct);
                    if (result.Success && File.Exists(LiveBufferPath))
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            try
                            {
                                var bytes = File.ReadAllBytes(LiveBufferPath);
                                using var ms = new MemoryStream(bytes);
                                Screenshot = new Bitmap(ms);
                                UpdateDiagnostic(Screenshot);
                            }
                            catch { /* 帧跳过，不记录噪音 */ }
                        });
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { /* 帧跳过 */ }

                try { await Task.Delay(500, ct); }
                catch (OperationCanceledException) { break; }
            }
        }, ct);

        await Task.CompletedTask;
    }

    public async Task ClickAtAsync(int x, int y)
    {
        IsBusy = true;
        _log.Info($"截图点击坐标: ({x}, {y})");
        try
        {
            var result = await _controller.ClickAsync(x, y);
            _log.Info(result.Success ? "点击成功" : "点击失败");
        }
        catch (Exception ex)
        {
            _log.Error($"点击异常: {ex.Message}");
        }
        IsBusy = false;
    }

    public async Task ScrollAtAsync(int x, int y, int delta)
    {
        IsBusy = true;
        var dir = delta > 0 ? "↑ 向上" : "↓ 向下";
        _log.Info($"截图滚轮坐标: ({x}, {y}), delta: {delta} ({dir})");
        try
        {
            var result = await _controller.ScrollAsync(x, y, delta);
            _log.Info(result.Success ? "滚动成功" : "滚动失败");
        }
        catch (Exception ex)
        {
            _log.Error($"滚动异常: {ex.Message}");
        }
        IsBusy = false;
    }

    [RelayCommand]
    private async Task RunPipelineAsync()
    {
        IsBusy = true;
        _log.Info($"执行 Pipeline: {PipelineName}");
        try
        {
            var result = await _controller.RunPipelineAsync(PipelineName);
            _log.Info($"Pipeline 结果: Success={result.Success}, NodeHit={result.NodeHit}");
        }
        catch (Exception ex)
        {
            _log.Error($"Pipeline 异常: {ex.Message}");
        }
        IsBusy = false;
    }

    [RelayCommand]
    private async Task ReloadResourcesAsync()
    {
        IsBusy = true;
        _log.Info("重载资源...");
        try
        {
            await _controller.ReloadResourcesAsync();
            _log.Info("资源重载完成");
        }
        catch (Exception ex)
        {
            _log.Error($"重载异常: {ex.Message}");
        }
        IsBusy = false;
    }

    [RelayCommand]
    private void ClearLog()
    {
        _log.Clear();
    }

    #endregion

    #region Flow Methods (直接方法调用，支持条件判断)

    [RelayCommand]
    private async Task RunMessageButtonFlowAsync()
    {
        if (IsFlowRunning) return;

        IsFlowRunning = true;
        _flowCts = new CancellationTokenSource();
        var ct = _flowCts.Token;
        var screenshotDir = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(screenshotDir);

        try
        {
            // ========== STEP 1: 初始截图 ==========
            _log.Info("[Flow] STEP 1/4: 初始截图...");
            var beforePath = Path.Combine(screenshotDir, $"flow_before_{DateTime.Now:HHmmss}.png");
            var sc1 = await _controller.ScreenshotAsync(beforePath, ct);
            if (!sc1.Success) throw new InvalidOperationException("初始截图失败");
            _log.Info($"[Flow] 截图已保存: {beforePath}");

            // TODO: 条件判断 - 检测当前页面
            // 例如：通过 TemplateMatch 或 OCR 判断是否已经在消息页面
            // 如果在消息页面，可以跳过点击，直接裁剪
            // 这里先简单实现：无条件点击

            // ========== STEP 2: 点击消息按钮 ==========
            await ClickMessageButtonAsync(ct);

            // ========== STEP 3: 点击后截图 ==========
            _log.Info("[Flow] STEP 3/4: 点击后截图...");
            var afterPath = Path.Combine(screenshotDir, $"flow_after_{DateTime.Now:HHmmss}.png");
            var sc2 = await _controller.ScreenshotAsync(afterPath, ct);
            if (!sc2.Success) throw new InvalidOperationException("点击后截图失败");
            _log.Info($"[Flow] 截图已保存: {afterPath}");

            // ========== STEP 4: 裁剪区域 ==========
            _log.Info("[Flow] STEP 4/5: 按百分比裁剪聊天列表区域...");

            var cropResult = await CropScreenshotByPercentAsync(afterPath,
                pctX1: 0.097, pctY1: 0.223,
                pctX2: 0.393, pctY2: 0.993, ct);
            _log.Info($"[Flow] 裁剪完成: {cropResult}");

            // ========== STEP 5: 在裁剪区域内向下滚动 ==========
            _log.Info("[Flow] STEP 5/5: 在聊天列表区域内向下滚动...");
            var (ctrlW, ctrlH) = _controller.Resolution;
            // 滚动位置取裁剪区域中心
            int scrollX = (int)(0.245 * ctrlW);  // (9.7% + 39.3%) / 2
            int scrollY = (int)(0.608 * ctrlH);  // (22.3% + 99.3%) / 2
            _log.Info($"[Flow] 滚动位置: ({scrollX}, {scrollY})，delta: -720");
            await _controller.ScrollAsync(scrollX, scrollY, delta: 720, ct);
            _log.Info("[Flow] 滚动完成");
            await Task.Delay(500, ct);

            _log.Info("[Flow] ✅ 流程执行完成");
        }
        catch (OperationCanceledException)
        {
            _log.Info("[Flow] 流程已取消");
        }
        catch (Exception ex)
        {
            _log.Error($"[Flow] 流程执行异常: {ex.Message}");
        }
        finally
        {
            IsFlowRunning = false;
            _flowCts = null;
        }
    }

    /// <summary>
    /// 基本动作：点击左侧导航的"消息"按钮。
    /// 范围：左上 1.3%,22.7% → 右下 7.7%,26.4%。
    /// 每次在范围内随机选点，规避反自动化检测。
    /// </summary>
    private async Task ClickMessageButtonAsync(CancellationToken ct)
    {
        _log.Info("[Action] 点击消息按钮...");
        var (ctrlW, ctrlH) = _controller.Resolution;

        double pctX = Random.Shared.NextDouble() * (0.077 - 0.013) + 0.013;
        double pctY = Random.Shared.NextDouble() * (0.264 - 0.227) + 0.227;

        int x = (int)(pctX * ctrlW);
        int y = (int)(pctY * ctrlH);

        _log.Info($"[Action] 点击坐标: ({x}, {y}) [百分比: {pctX:P2}, {pctY:P2}]");
        var result = await _controller.ClickAsync(x, y, ct);
        if (!result.Success) throw new InvalidOperationException("点击消息按钮失败");
        _log.Info("[Action] 点击成功");
        await Task.Delay(500, ct);
    }

    /// <summary>
    /// 基本动作：点击"全部"消息按钮。
    /// 范围：左上 11.4%,18.4% → 右下 12.5%,19.2%。
    /// 每次在范围内随机选点，规避反自动化检测。
    /// </summary>
    private async Task ClickAllMessagesButtonAsync(CancellationToken ct)
    {
        _log.Info("[Action] 点击全部消息按钮...");
        var (ctrlW, ctrlH) = _controller.Resolution;

        double pctX = Random.Shared.NextDouble() * (0.125 - 0.114) + 0.114;
        double pctY = Random.Shared.NextDouble() * (0.192 - 0.184) + 0.184;

        int x = (int)(pctX * ctrlW);
        int y = (int)(pctY * ctrlH);

        _log.Info($"[Action] 点击坐标: ({x}, {y}) [百分比: {pctX:P2}, {pctY:P2}]");
        var result = await _controller.ClickAsync(x, y, ct);
        if (!result.Success) throw new InvalidOperationException("点击全部消息按钮失败");
        _log.Info("[Action] 点击成功");
        await Task.Delay(500, ct);
    }

    /// <summary>
    /// 基本动作：点击"未读"消息按钮。
    /// 范围：左上 14.7%,18.4% → 右下 16.0%,19.2%。
    /// 每次在范围内随机选点，规避反自动化检测。
    /// </summary>
    private async Task ClickUnreadMessagesButtonAsync(CancellationToken ct)
    {
        _log.Info("[Action] 点击未读消息按钮...");
        var (ctrlW, ctrlH) = _controller.Resolution;

        double pctX = Random.Shared.NextDouble() * (0.160 - 0.147) + 0.147;
        double pctY = Random.Shared.NextDouble() * (0.192 - 0.184) + 0.184;

        int x = (int)(pctX * ctrlW);
        int y = (int)(pctY * ctrlH);

        _log.Info($"[Action] 点击坐标: ({x}, {y}) [百分比: {pctX:P2}, {pctY:P2}]");
        var result = await _controller.ClickAsync(x, y, ct);
        if (!result.Success) throw new InvalidOperationException("点击未读消息按钮失败");
        _log.Info("[Action] 点击成功");
        await Task.Delay(500, ct);
    }

    [RelayCommand]
    private void StopFlow()
    {
        _flowCts?.Cancel();
        _log.Info("正在取消流程...");
    }

    /// <summary>
    /// 裁剪截图的指定窗体区域为图片像素（按百分比）。
    /// </summary>
    private async Task<string> CropScreenshotByPercentAsync(string screenshotPath,
        double pctX1, double pctY1, double pctX2, double pctY2,
        CancellationToken ct)
    {
        var outputPath = Path.Combine(
            Path.GetDirectoryName(screenshotPath)!,
            $"crop_{Path.GetFileNameWithoutExtension(screenshotPath)}.png");

        var scriptPath = Path.Combine(Path.GetTempPath(), $"crop_{Guid.NewGuid():N}.py");
        var scriptContent = string.Join("\n", new[]
        {
            "from PIL import Image",
            $"img = Image.open(r'{screenshotPath.Replace("\\", "/")}')",
            "img_w, img_h = img.size",
            $"x1 = int({pctX1:F4} * img_w)",
            $"y1 = int({pctY1:F4} * img_h)",
            $"x2 = int({pctX2:F4} * img_w)",
            $"y2 = int({pctY2:F4} * img_h)",
            "crop = img.crop((x1, y1, x2, y2))",
            $"crop.save(r'{outputPath.Replace("\\", "/")}')",
            "print('CROPPED:' + str(crop.size))",
        });
        await File.WriteAllTextAsync(scriptPath, scriptContent, ct);

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "python",
                Arguments = scriptPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            if (stdout.Contains("CROPPED:"))
                _log.Info($"[Crop] {stdout.Trim()}");
            if (!string.IsNullOrWhiteSpace(stderr))
                _log.Warn($"[Crop-ERR] {stderr.Trim()}");
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { }
        }

        return outputPath;
    }

    #endregion
}
