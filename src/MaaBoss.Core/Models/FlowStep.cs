using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MaaBoss.Core.Models;

public partial class FlowStep : ObservableObject
{
    [ObservableProperty]
    public partial Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    public partial string Name { get; set; } = "新步骤";

    [ObservableProperty]
    public partial FlowActionType Action { get; set; } = FlowActionType.Click;

    // ---- 识别参数 ----

    [ObservableProperty]
    public partial FlowRecognitionType Recognition { get; set; } = FlowRecognitionType.DirectHit;

    [ObservableProperty]
    public partial string Template { get; set; } = "";

    /// <summary>OCR 期望文字，多行用换行分隔</summary>
    [ObservableProperty]
    public partial string Expected { get; set; } = "";

    [ObservableProperty]
    public partial int RoiX { get; set; }

    [ObservableProperty]
    public partial int RoiY { get; set; }

    [ObservableProperty]
    public partial int RoiW { get; set; }

    [ObservableProperty]
    public partial int RoiH { get; set; }

    [ObservableProperty]
    public partial double Threshold { get; set; } = 0.8;

    [ObservableProperty]
    public partial bool Inverse { get; set; }

    [ObservableProperty]
    public partial int Timeout { get; set; } = 20000;

    // ---- 操作参数 ----

    [ObservableProperty]
    public partial int X { get; set; }

    [ObservableProperty]
    public partial int Y { get; set; }

    [ObservableProperty]
    public partial int X2 { get; set; }

    [ObservableProperty]
    public partial int Y2 { get; set; }

    [ObservableProperty]
    public partial int DurationMs { get; set; } = 1000;

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    [ObservableProperty]
    public partial int ScrollDelta { get; set; } = 120;

    [ObservableProperty]
    public partial string PipelineName { get; set; } = "";

    // ---- Pipeline 通用参数 ----

    [ObservableProperty]
    public partial int PreDelay { get; set; } = 200;

    [ObservableProperty]
    public partial int PostDelay { get; set; } = 500;

    [ObservableProperty]
    public partial int TargetOffsetX { get; set; }

    [ObservableProperty]
    public partial int TargetOffsetY { get; set; }

    // ---- 高级识别类型的原始 JSON 参数（NeuralNetwork/And/Or/Custom 等） ----

    [ObservableProperty]
    public partial string CustomRecognitionJson { get; set; } = "";
}
