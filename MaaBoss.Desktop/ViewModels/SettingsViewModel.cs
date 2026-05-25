using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaaFramework.Binding;

namespace MaaBoss.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    /// <summary>
    /// 控制器预设：打包截图方式 + 鼠标方式 + 键盘方式。
    /// 设计参考 MaaEnd 的 interface.json 控制器配置。
    /// </summary>
    public record ControllerPreset(
        string Name,
        Win32ScreencapMethod Screencap,
        Win32InputMethod Mouse,
        Win32InputMethod Keyboard);

    public ObservableCollection<ControllerPreset> ControllerPresets { get; } = new()
    {
        new("后台窗口 (DXGI)", Win32ScreencapMethod.DXGI_DesktopDup_Window, Win32InputMethod.SendMessageWithCursorPos, Win32InputMethod.PostMessage),
        new("后台窗口 (PrintWindow)", Win32ScreencapMethod.PrintWindow, Win32InputMethod.SendMessageWithWindowPos, Win32InputMethod.PostMessage),
        new("前台独占", Win32ScreencapMethod.ScreenDC, Win32InputMethod.Seize, Win32InputMethod.Seize),
    };

    [ObservableProperty]
    public partial ControllerPreset SelectedControllerPreset { get; set; } = new(
        "后台窗口 (DXGI)", Win32ScreencapMethod.DXGI_DesktopDup_Window, Win32InputMethod.SendMessageWithCursorPos, Win32InputMethod.PostMessage);

    [ObservableProperty]
    public partial string Win32WindowName { get; set; } = "BOSS直聘";

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
        SelectedControllerPreset = ControllerPresets[0];
        Win32WindowName = "BOSS直聘";
        AdbPath = "adb";
        AdbAddress = "127.0.0.1:5555";
        RateLimitMs = 1000;
        TimeoutMs = 20000;
        DailyGreetLimit = 100;
        SaveDebugDraw = true;
        McpServerUrl = "http://localhost:5000";
    }
}
