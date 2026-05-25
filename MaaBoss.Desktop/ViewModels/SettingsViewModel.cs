using System.Collections.ObjectModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaaFramework.Binding;

namespace MaaBoss.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public record ScreencapMethodOption(string Name, Win32ScreencapMethod Value);

    public ObservableCollection<ScreencapMethodOption> ScreencapMethodOptions { get; } = new()
    {
        new("DXGI 窗口裁剪 (推荐)", Win32ScreencapMethod.DXGI_DesktopDup_Window),
        new("DXGI 桌面复制", Win32ScreencapMethod.DXGI_DesktopDup),
        new("GDI 位图复制", Win32ScreencapMethod.GDI),
        new("PrintWindow", Win32ScreencapMethod.PrintWindow),
        new("屏幕 DC", Win32ScreencapMethod.ScreenDC),
    };

    [ObservableProperty]
    public partial ScreencapMethodOption SelectedScreencapMethodOption { get; set; } = new("DXGI 窗口裁剪 (推荐)", Win32ScreencapMethod.DXGI_DesktopDup_Window);

    [ObservableProperty]
    public partial string Win32WindowName { get; set; } = "boss-zhipin.exe";

    [ObservableProperty]
    public partial string AdbPath { get; set; } = "adb";

    [ObservableProperty]
    public partial string AdbAddress { get; set; } = "127.0.0.1:5555";

    [ObservableProperty]
    public partial int RateLimitMs { get; set; } = 1000;

    [ObservableProperty]
    public partial int TimeoutMs { get; set; } = 20000;

    [ObservableProperty]
    public partial int DailyGreetLimit { get; set; } = 100;

    [ObservableProperty]
    public partial bool SaveDebugDraw { get; set; } = true;

    [ObservableProperty]
    public partial string McpServerUrl { get; set; } = "http://localhost:5000";

    [RelayCommand]
    private void SaveSettings()
    {
        // TODO: 持久化到配置文件
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        SelectedScreencapMethodOption = ScreencapMethodOptions[0];
        Win32WindowName = "boss-zhipin.exe";
        AdbPath = "adb";
        AdbAddress = "127.0.0.1:5555";
        RateLimitMs = 1000;
        TimeoutMs = 20000;
        DailyGreetLimit = 100;
        SaveDebugDraw = true;
        McpServerUrl = "http://localhost:5000";
    }
}
