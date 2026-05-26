using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaaBoss.Core.Models;
using MaaBoss.Core.Services;
using MaaBoss.Desktop.Infrastructure;

namespace MaaBoss.Desktop.ViewModels;

public partial class FlowEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial ObservableCollection<FlowStep> Steps { get; set; } = new();

    [ObservableProperty]
    public partial FlowStep? SelectedStep { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "就绪";

    [ObservableProperty]
    public partial string FlowName { get; set; } = "未命名流程";

    public FlowActionType[] ActionTypes { get; } = Enum.GetValues<FlowActionType>();

    private readonly ControllerService _controller;
    private readonly LogService _log;
    private CancellationTokenSource? _runCts;

    public FlowEditorViewModel()
    {
        _controller = ServiceLocator.Get<ControllerService>();
        _log = ServiceLocator.Get<LogService>();
    }

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
            StatusText = "流程为空";
            return;
        }

        IsRunning = true;
        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;

        try
        {
            for (int i = 0; i < Steps.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                var step = Steps[i];
                StatusText = $"执行 [{i + 1}/{Steps.Count}]: {step.Name}";
                await ExecuteStepAsync(step, ct);
                _log.Info($"步骤完成: {step.Name}");
            }
            StatusText = "流程执行完成";
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消";
        }
        catch (Exception ex)
        {
            StatusText = $"执行出错: {ex.Message}";
            _log.Error($"流程执行异常: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            _runCts = null;
        }
    }

    [RelayCommand]
    private void StopFlow()
    {
        _runCts?.Cancel();
        StatusText = "正在取消...";
    }

    private async Task ExecuteStepAsync(FlowStep step, CancellationToken ct)
    {
        switch (step.Action)
        {
            case FlowActionType.Click:
                await _controller.ClickAsync(step.X, step.Y, ct);
                break;
            case FlowActionType.Swipe:
                await _controller.SwipeAsync(step.X, step.Y, step.X2, step.Y2, step.DurationMs, ct);
                break;
            case FlowActionType.Wait:
                await Task.Delay(step.DurationMs, ct);
                break;
            case FlowActionType.Delay:
                await Task.Delay(step.DurationMs, ct);
                break;
            case FlowActionType.InputText:
                // MaaFramework 没有直接输入文本的 API，暂时用 Click 代替或留空
                _log.Warn("输入文本功能暂不支持");
                break;
            case FlowActionType.Screenshot:
                var path = Path.Combine(AppContext.BaseDirectory, "screenshots", $"flow_{DateTime.Now:HHmmss}.png");
                await _controller.ScreenshotAsync(path, ct);
                break;
            case FlowActionType.Pipeline:
                if (!string.IsNullOrWhiteSpace(step.PipelineName))
                    await _controller.RunPipelineAsync(step.PipelineName, null, ct);
                break;
            default:
                _log.Warn($"未知操作类型: {step.Action}");
                break;
        }
    }

    [RelayCommand]
    private void SaveFlow()
    {
        try
        {
            var dto = new FlowDto(FlowName, Steps.ToList());
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            var dir = Path.Combine(AppContext.BaseDirectory, "flows");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{FlowName}.json");
            File.WriteAllText(path, json);
            StatusText = $"已保存: {path}";
            _log.Info($"流程已保存: {path}");
        }
        catch (Exception ex)
        {
            StatusText = $"保存失败: {ex.Message}";
            _log.Error($"保存流程失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void LoadFlow()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "flows");
            if (!Directory.Exists(dir)) return;
            var files = Directory.GetFiles(dir, "*.json");
            if (files.Length == 0)
            {
                StatusText = "没有找到流程文件";
                return;
            }
            // 暂时加载第一个文件，后续可以添加选择对话框
            var path = files[0];
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<FlowDto>(json);
            if (dto == null) return;
            FlowName = dto.Name;
            Steps = new ObservableCollection<FlowStep>(dto.Steps);
            SelectedStep = Steps.FirstOrDefault();
            StatusText = $"已加载: {path}";
            _log.Info($"流程已加载: {path}");
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
            _log.Error($"加载流程失败: {ex.Message}");
        }
    }

    private record FlowDto(string Name, System.Collections.Generic.List<FlowStep> Steps);
}
