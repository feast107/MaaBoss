using System;
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

    // 流程编辑
    [ObservableProperty]
    public partial ObservableCollection<FlowStep> Steps { get; set; } = new();

    [ObservableProperty]
    public partial FlowStep? SelectedStep { get; set; }

    [ObservableProperty]
    public partial bool IsFlowRunning { get; set; }

    [ObservableProperty]
    public partial string FlowName { get; set; } = "未命名流程";

    // 已保存的流程列表
    [ObservableProperty]
    public partial ObservableCollection<string> SavedFlows { get; set; } = new();

    [ObservableProperty]
    public partial string? SelectedSavedFlow { get; set; }

    public FlowActionType[] ActionTypes { get; } = Enum.GetValues<FlowActionType>();
    public FlowRecognitionType[] RecognitionTypes { get; } = Enum.GetValues<FlowRecognitionType>();

    private readonly ControllerService _controller;
    private readonly LogService _log;
    private CancellationTokenSource? _flowCts;

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

        RefreshFlows();
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
                UpdateDiagnostic(Screenshot);
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

    #region Flow Editor

    [RelayCommand]
    private void AddStep(FlowActionType? actionType)
    {
        var type = actionType ?? FlowActionType.Click;
        var step = new FlowStep
        {
            Action = type,
            Name = type switch
            {
                FlowActionType.Click => "点击",
                FlowActionType.Swipe => "滑动",
                FlowActionType.Scroll => "滚轮",
                FlowActionType.Wait => "等待",
                FlowActionType.InputText => "输入文本",
                FlowActionType.Screenshot => "截图",
                FlowActionType.Pipeline => "执行 Pipeline",
                FlowActionType.Delay => "延时",
                _ => "步骤"
            }
        };
        Steps.Add(step);
        SelectedStep = step;
    }

    [RelayCommand]
    private void RemoveStep()
    {
        if (SelectedStep == null) return;
        Steps.Remove(SelectedStep);
        SelectedStep = Steps.LastOrDefault();
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedStep == null) return;
        var idx = Steps.IndexOf(SelectedStep);
        if (idx <= 0) return;
        Steps.Move(idx, idx - 1);
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedStep == null) return;
        var idx = Steps.IndexOf(SelectedStep);
        if (idx < 0 || idx >= Steps.Count - 1) return;
        Steps.Move(idx, idx + 1);
    }

    [RelayCommand]
    private async Task RunFlowAsync()
    {
        if (Steps.Count == 0)
        {
            _log.Warn("流程为空，无法执行");
            return;
        }

        IsFlowRunning = true;
        _flowCts = new CancellationTokenSource();
        var ct = _flowCts.Token;

        try
        {
            for (int i = 0; i < Steps.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                var step = Steps[i];
                _log.Info($"流程 [{i + 1}/{Steps.Count}]: {step.Name} ({step.Action})");
                await ExecuteFlowStepAsync(step, ct);
            }
            _log.Info("流程执行完成");
        }
        catch (OperationCanceledException)
        {
            _log.Info("流程已取消");
        }
        catch (Exception ex)
        {
            _log.Error($"流程执行异常: {ex.Message}");
        }
        finally
        {
            IsFlowRunning = false;
            _flowCts = null;
        }
    }

    [RelayCommand]
    private void StopFlow()
    {
        _flowCts?.Cancel();
        _log.Info("正在取消流程...");
    }

    private async Task ExecuteFlowStepAsync(FlowStep step, CancellationToken ct)
    {
        switch (step.Action)
        {
            case FlowActionType.Scroll:
                _log.Info($"滚轮: ({step.X}, {step.Y}) delta={step.ScrollDelta}");
                await _controller.ScrollAsync(step.X, step.Y, step.ScrollDelta, ct);
                break;

            case FlowActionType.Screenshot:
                var path = Path.Combine(AppContext.BaseDirectory, "screenshots", $"flow_{DateTime.Now:HHmmss}.png");
                _log.Info($"截图: {path}");
                await _controller.ScreenshotAsync(path, ct);
                break;

            case FlowActionType.Pipeline:
                if (!string.IsNullOrWhiteSpace(step.PipelineName))
                {
                    _log.Info($"执行 Pipeline: {step.PipelineName}");
                    await _controller.RunPipelineAsync(step.PipelineName, null, ct);
                }
                break;

            default:
                await ExecutePipelineStepAsync(step, ct);
                break;
        }
    }

    private async Task ExecutePipelineStepAsync(FlowStep step, CancellationToken ct)
    {
        var nodeName = $"FlowStep_{step.Id:N}";
        var pipelineOverride = BuildPipelineNode(step);
        var wrapper = new JObject { [nodeName] = pipelineOverride };

        _log.Info($"Pipeline 节点: {nodeName} | 识别: {step.Recognition} | 动作: {step.Action}");
        if (!string.IsNullOrWhiteSpace(step.Template))
            _log.Info($"  模板: {step.Template}");
        if (!string.IsNullOrWhiteSpace(step.Expected))
            _log.Info($"  期望: {step.Expected.Replace('\n', ',')}");

        try
        {
            var result = await _controller.RunPipelineAsync(nodeName, wrapper, ct);
            _log.Info($"Pipeline 结果: Success={result.Success}, NodeHit={result.NodeHit}");
        }
        catch (Exception ex)
        {
            _log.Error($"Pipeline 执行失败: {ex.Message}");
            throw;
        }
    }

    private JObject BuildPipelineNode(FlowStep step, string? nextNodeName = null)
    {
        var node = new JObject();

        // 识别方式
        node["recognition"] = step.Recognition.ToString();

        // 识别参数
        if ((step.Recognition == FlowRecognitionType.TemplateMatch || step.Recognition == FlowRecognitionType.FeatureMatch)
            && !string.IsNullOrWhiteSpace(step.Template))
            node["template"] = step.Template;

        if (step.Recognition == FlowRecognitionType.OCR && !string.IsNullOrWhiteSpace(step.Expected))
        {
            var lines = step.Expected.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
            if (lines.Length > 0)
                node["expected"] = new JArray(lines);
        }

        if (step.RoiW > 0 && step.RoiH > 0)
            node["roi"] = new JArray(step.RoiX, step.RoiY, step.RoiW, step.RoiH);

        if (Math.Abs(step.Threshold - 0.8) > 0.001)
            node["threshold"] = step.Threshold;

        if (step.Inverse)
            node["inverse"] = true;

        if (step.Timeout != 20000)
            node["timeout"] = step.Timeout;

        // 高级识别类型的自定义 JSON 参数
        if (!string.IsNullOrWhiteSpace(step.CustomRecognitionJson))
        {
            try
            {
                var custom = JObject.Parse(step.CustomRecognitionJson);
                foreach (var prop in custom.Properties())
                    node[prop.Name] = prop.Value;
            }
            catch (Exception ex)
            {
                _log.Warn($"步骤 '{step.Name}' 的自定义识别 JSON 解析失败: {ex.Message}");
            }
        }

        // Action
        switch (step.Action)
        {
            case FlowActionType.Click:
                node["action"] = "Click";
                node["target"] = new JArray(step.X, step.Y, 0, 0);
                if (step.TargetOffsetX != 0 || step.TargetOffsetY != 0)
                    node["target_offset"] = new JArray(step.TargetOffsetX, step.TargetOffsetY, 0, 0);
                break;

            case FlowActionType.Swipe:
                node["action"] = "Swipe";
                node["swipe"] = new JArray(step.X, step.Y, step.X2, step.Y2);
                break;

            case FlowActionType.InputText:
                node["action"] = "Input";
                node["input"] = step.Text;
                break;

            case FlowActionType.Wait:
            case FlowActionType.Delay:
                node["action"] = "DoNothing";
                node["post_delay"] = step.DurationMs;
                break;

            case FlowActionType.Scroll:
                node["action"] = "DoNothing";
                node["_comment"] = $"Scroll: ({step.X},{step.Y}) delta={step.ScrollDelta}";
                break;

            case FlowActionType.Screenshot:
                node["action"] = "DoNothing";
                node["_comment"] = "Screenshot";
                break;
        }

        // 通用延迟参数（Wait/Delay 使用 DurationMs 作为 post_delay）
        if (step.Action != FlowActionType.Wait && step.Action != FlowActionType.Delay)
        {
            if (step.PreDelay != 200)
                node["pre_delay"] = step.PreDelay;
            if (step.PostDelay != 500)
                node["post_delay"] = step.PostDelay;
        }

        if (nextNodeName != null)
            node["next"] = new JArray(nextNodeName);
        else
            node["next"] = new JArray();

        return node;
    }

    [RelayCommand]
    private void ExportPipeline()
    {
        try
        {
            if (Steps.Count == 0)
            {
                _log.Warn("流程为空，无需导出");
                return;
            }

            var pipeline = new JObject
            {
                ["_doc"] = $"由 MaaBoss Flow '{FlowName}' 导出，兼容 MaaPipelineEditor"
            };

            for (int i = 0; i < Steps.Count; i++)
            {
                var step = Steps[i];
                var nodeName = $"Step_{i}";
                var nextName = i < Steps.Count - 1 ? $"Step_{i + 1}" : null;
                pipeline[nodeName] = BuildPipelineNode(step, nextName);
            }

            var json = pipeline.ToString(Newtonsoft.Json.Formatting.Indented);
            var dir = Path.Combine(AppContext.BaseDirectory, "flows");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{FlowName}_pipeline.json");
            File.WriteAllText(path, json);
            _log.Info($"Pipeline JSON 已导出: {path}");
        }
        catch (Exception ex)
        {
            _log.Error($"导出 Pipeline 失败: {ex.Message}");
        }
    }

    private void RefreshFlows()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "flows");
        if (!Directory.Exists(dir))
        {
            SavedFlows.Clear();
            return;
        }
        var files = Directory.GetFiles(dir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .OrderBy(n => n)
            .ToList();
        SavedFlows = new ObservableCollection<string>(files);
    }

    [RelayCommand]
    private void RefreshFlowsCommand() => RefreshFlows();

    [RelayCommand]
    private void SaveFlow()
    {
        try
        {
            var dto = new FlowDto(FlowName, Steps.ToList());
            var json = System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var dir = Path.Combine(AppContext.BaseDirectory, "flows");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{FlowName}.json");
            File.WriteAllText(path, json);
            _log.Info($"流程已保存: {path}");
            RefreshFlows();
        }
        catch (Exception ex)
        {
            _log.Error($"保存流程失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void LoadFlow(string? flowName = null)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "flows");
            if (!Directory.Exists(dir)) return;

            var name = flowName ?? SelectedSavedFlow;
            if (string.IsNullOrWhiteSpace(name))
            {
                _log.Warn("请选择要加载的流程");
                return;
            }

            var path = Path.Combine(dir, $"{name}.json");
            if (!File.Exists(path))
            {
                _log.Warn($"流程文件不存在: {path}");
                return;
            }

            var json = File.ReadAllText(path);
            var dto = System.Text.Json.JsonSerializer.Deserialize<FlowDto>(json);
            if (dto == null) return;
            FlowName = dto.Name;
            Steps = new ObservableCollection<FlowStep>(dto.Steps);
            SelectedStep = Steps.FirstOrDefault();
            _log.Info($"流程已加载: {name}");
        }
        catch (Exception ex)
        {
            _log.Error($"加载流程失败: {ex.Message}");
        }
    }

    private record FlowDto(string Name, System.Collections.Generic.List<FlowStep> Steps);

    #endregion
}
