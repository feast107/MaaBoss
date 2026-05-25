using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MaaBoss.Desktop.Messaging;
using MaaBoss.Desktop.Services;

namespace MaaBoss.Desktop.ViewModels;

public partial class DebugViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial Bitmap? Screenshot { get; set; }

    [ObservableProperty]
    public partial string ClickX { get; set; } = "960";

    [ObservableProperty]
    public partial string ClickY { get; set; } = "540";

    [ObservableProperty]
    public partial string SwipeX1 { get; set; } = "960";

    [ObservableProperty]
    public partial string SwipeY1 { get; set; } = "800";

    [ObservableProperty]
    public partial string SwipeX2 { get; set; } = "960";

    [ObservableProperty]
    public partial string SwipeY2 { get; set; } = "300";

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

    private readonly ControllerService _controller;
    private readonly LogService _log;

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

    public void UpdateCursorPosition(int imgX, int imgY, int targetX, int targetY, double scaling = 1.0)
    {
        if (imgX < 0)
        {
            CursorPosText = "鼠标在截图区域内移动以查看坐标";
            return;
        }

        if (scaling > 1.0)
            CursorPosText = $"截图内: ({imgX}, {imgY})  →  窗体: ({targetX}, {targetY})  [DPI: {scaling:F1}x]";
        else
            CursorPosText = $"截图内: ({imgX}, {imgY})  →  窗体: ({targetX}, {targetY})";
    }

    [RelayCommand]
    private async Task TakeScreenshotAsync()
    {
        IsBusy = true;
        _log.Info("开始截图...");
        try
        {
            var screenshotsDir = Path.Combine(AppContext.BaseDirectory, "screenshots");
            if (!Directory.Exists(screenshotsDir))
                Directory.CreateDirectory(screenshotsDir);
            var path = Path.Combine(screenshotsDir, $"maaboss_screenshot_{DateTime.Now:HHmmss}.png");
            var result = await _controller.ScreenshotAsync(path);
            if (result.Success && File.Exists(path))
            {
                await using var stream = File.OpenRead(path);
                Screenshot = new Bitmap(stream);
                _log.Info($"截图已保存: {path}");
                WeakReferenceMessenger.Default.Send(new ScreenshotTakenMessage(path));
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

    public async Task ClickAtAsync(int x, int y)
    {
        IsBusy = true;
        ClickX = x.ToString();
        ClickY = y.ToString();
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

    [RelayCommand]
    private async Task ClickAsync()
    {
        if (!int.TryParse(ClickX, out var x) || !int.TryParse(ClickY, out var y)) return;
        await ClickAtAsync(x, y);
    }

    [RelayCommand]
    private async Task SwipeAsync()
    {
        if (!int.TryParse(SwipeX1, out var x1) || !int.TryParse(SwipeY1, out var y1) ||
            !int.TryParse(SwipeX2, out var x2) || !int.TryParse(SwipeY2, out var y2)) return;
        IsBusy = true;
        _log.Info($"滑动: ({x1},{y1}) -> ({x2},{y2})");
        try
        {
            var result = await _controller.SwipeAsync(x1, y1, x2, y2, 500);
            _log.Info(result.Success ? "滑动成功" : "滑动失败");
        }
        catch (Exception ex)
        {
            _log.Error($"滑动异常: {ex.Message}");
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
}
