using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MaaBoss.Desktop.Models;
using MaaBoss.Desktop.Messaging;
using MaaBoss.Desktop.Services;

namespace MaaBoss.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly TaskService _tasks;

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
    }

    [RelayCommand]
    private async Task ConnectWin32Async()
    {
        IsBusy = true;
        StatusText = "正在连接 Win32...";
        try
        {
            var result = await _tasks.LaunchAppAsync("win32", null, true, default);
            UpdateStatus(result);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ConnectAdbAsync()
    {
        IsBusy = true;
        StatusText = "正在连接 ADB...";
        try
        {
            var result = await _tasks.LaunchAppAsync("adb", "127.0.0.1:5555", true, default);
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
