using CommunityToolkit.Mvvm.ComponentModel;

namespace MaaBoss.Desktop.Models;

public partial class AppState : ObservableObject
{
    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial string ControllerType { get; set; } = "未连接";

    [ObservableProperty]
    public partial string Resolution { get; set; } = "-";

    [ObservableProperty]
    public partial string CurrentScreen { get; set; } = "-";

    [ObservableProperty]
    public partial int TodayGreeted { get; set; }

    [ObservableProperty]
    public partial int DailyLimit { get; set; } = 100;

    [ObservableProperty]
    public partial string LogText { get; set; } = "";
}
