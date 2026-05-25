using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MaaBoss.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
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
