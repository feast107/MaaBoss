using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MaaBoss.Core.Models;
using MaaBoss.Core.Messaging;
using MaaBoss.Core.Services;
using MaaBoss.Desktop.Infrastructure;

namespace MaaBoss.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly TaskService _tasks;
    private readonly SettingsViewModel _settings;

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial string ControllerType { get; set; } = "未连接";

    [ObservableProperty]
    public partial string Resolution { get; set; } = "-";

    [ObservableProperty]
    public partial string StatusText { get; set; } = "等待连接...";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public DashboardViewModel()
    {
        _tasks = ServiceLocator.GetRequiredService<TaskService>();
        _settings = ServiceLocator.Get<SettingsViewModel>();
    }

    [RelayCommand]
    private async Task ConnectWin32Async()
    {
        IsBusy = true;
        StatusText = "正在连接 Win32...";
        try
        {
            var preset = _settings.SelectedControllerPreset;
            var result = await _tasks.LaunchAppAsync(true, preset.Screencap, preset.Mouse, preset.Keyboard, _settings.Win32WindowName, default);
            UpdateStatus(result);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        IsBusy = true;
        try
        {
            await ServiceLocator.GetRequiredService<ControllerService>().DisconnectAsync(default);
            IsConnected = false;
            ControllerType = "未连接";
            Resolution = "-";
            StatusText = "已断开连接";
            WeakReferenceMessenger.Default.Send(new ConnectionStateChangedMessage(null));
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task QuickScreenshotAsync()
    {
        if (!IsConnected) { StatusText = "请先连接客户端"; return; }
        IsBusy = true;
        try
        {
            var result = await ServiceLocator.GetRequiredService<ControllerService>().ScreenshotAsync(null, default);
            StatusText = result.Success ? $"截图已保存: {result.Path}" : $"截图失败: 控制器未连接或操作异常";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CheckJobPostsAsync()
    {
        if (!IsConnected) { StatusText = "请先连接客户端"; return; }
        IsBusy = true;
        try
        {
            var result = await _tasks.CheckJobPostsAsync(default);
            StatusText = result.Success
                ? $"职位查询完成，共 {(result.Extra?.TryGetValue("total_jobs", out var t) == true ? t : 0)} 个职位"
                : $"查询失败: {result.ErrorMessage}";
        }
        finally { IsBusy = false; }
    }

    private void UpdateStatus(ToolResult result)
    {
        if (result.Success)
        {
            IsConnected = true;
            ControllerType = result.Extra?.TryGetValue("controller_type", out var ct) == true ? ct?.ToString() ?? "win32" : "win32";
            Resolution = result.Extra?.TryGetValue("resolution", out var r) == true ? r?.ToString() ?? "-" : "-";
            StatusText = $"已连接 ({ControllerType}) {Resolution}";
            WeakReferenceMessenger.Default.Send(new ConnectionStateChangedMessage(
                new ConnectResult(true, ControllerType, Resolution)));
        }
        else
        {
            StatusText = $"连接失败: {result.ErrorMessage}";
        }
    }
}
