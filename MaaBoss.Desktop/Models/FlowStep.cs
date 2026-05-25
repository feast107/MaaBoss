using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MaaBoss.Desktop.Models;

public partial class FlowStep : ObservableObject
{
    [ObservableProperty]
    public partial Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    public partial string Name { get; set; } = "新步骤";

    [ObservableProperty]
    public partial FlowActionType Action { get; set; } = FlowActionType.Click;

    [ObservableProperty]
    public partial int X { get; set; }

    [ObservableProperty]
    public partial int Y { get; set; }

    [ObservableProperty]
    public partial int X2 { get; set; }

    [ObservableProperty]
    public partial int Y2 { get; set; }

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    [ObservableProperty]
    public partial int DurationMs { get; set; } = 1000;

    [ObservableProperty]
    public partial string PipelineName { get; set; } = "";
}
