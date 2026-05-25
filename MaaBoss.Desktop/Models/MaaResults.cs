namespace MaaBoss.Desktop.Models;

public record ConnectResult(bool Success, string ControllerType, string Resolution, string? ErrorMessage = null);
public record PipelineResult(bool Success, string NodeHit, object? Detail = null);
public record RecogResult(bool Success, int[]? Box = null, double Score = 0);
public record ScreenshotResult(bool Success, string? Path = null, string? Base64 = null);
public record ClickResult(bool Success, int X, int Y);
public record SwipeResult(bool Success, int X1, int Y1, int X2, int Y2);
public record InputResult(bool Success, string Action, object? Data = null);
