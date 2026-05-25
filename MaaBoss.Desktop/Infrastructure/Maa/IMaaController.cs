using System;
using System.Threading.Tasks;

namespace MaaBoss.Desktop.Infrastructure.Maa;

/// <summary>
/// MaaFramework 控制器抽象接口。
/// 真实实现由 MaaFramework.Binding.CSharp 提供，此处先以 Mock 运行。
/// </summary>
public interface IMaaController : IDisposable
{
    bool IsConnected { get; }
    (int Width, int Height) Resolution { get; }

    /// <summary>
    /// 连接到目标设备。
    /// </summary>
    /// <param name="type">控制器类型</param>
    /// <param name="adbAddress">ADB 设备地址</param>
    /// <param name="windowName">Win32 窗口名称/进程名（Win32 模式下使用）</param>
    Task<ConnectResult> ConnectAsync(ControllerType type, string? adbAddress = null, string? windowName = null);
    Task DisconnectAsync();

    Task<PipelineResult> RunPipelineAsync(string taskName, object? pipelineOverride = null);
    Task<RecogResult> RunRecognitionAsync(string imagePath, string recognitionType, int[]? roi = null);

    Task<ScreenshotResult> ScreenshotAsync(string? savePath = null);
    Task<ClickResult> ClickAsync(int x, int y);
    Task<SwipeResult> SwipeAsync(int x1, int y1, int x2, int y2, int durationMs = 500);
    Task<InputResult> InputTextAsync(string text);
    Task<InputResult> PressKeyAsync(int keycode);
    Task ReloadResourcesAsync();
}

public enum ControllerType { Win32, Adb }

public record ConnectResult(bool Success, string ControllerType, string Resolution, string? ErrorMessage = null);
public record PipelineResult(bool Success, string NodeHit, object? Detail = null);
public record RecogResult(bool Success, int[]? Box = null, double Score = 0);
public record ScreenshotResult(bool Success, string? Path = null, string? Base64 = null);
public record ClickResult(bool Success, int X, int Y);
public record SwipeResult(bool Success, int X1, int Y1, int X2, int Y2);
public record InputResult(bool Success, string Action, object? Data = null);
