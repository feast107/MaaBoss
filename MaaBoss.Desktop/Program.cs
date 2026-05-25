using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MaaBoss.Desktop.Infrastructure.Mcp;
using MaaBoss.Desktop.Services;

namespace MaaBoss.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var cts = new CancellationTokenSource();

        // 启动 Kestrel Web Server (MCP + API)
        var webTask = Task.Run(() => StartWebHost(args, cts.Token), cts.Token);

        // 启动 Avalonia Desktop UI
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        cts.Cancel();
        try { webTask.Wait(TimeSpan.FromSeconds(5)); } catch { /* ignored */ }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static void StartWebHost(string[] args, CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://localhost:5000");

        builder.Services.AddSingleton<ControllerService>();
        builder.Services.AddSingleton<TaskService>();
        builder.Services.AddSingleton<LogService>();

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        var app = builder.Build();

        // 健康检查
        app.MapGet("/", () => Results.Ok(new { status = "MaaBoss MCP Server running", version = "0.1.0" }));
        app.MapGet("/health", () => Results.Ok(new { healthy = true }));

        // MCP SSE 端点
        app.MapMcpSseEndpoints();

        // 启动（非阻塞监听 CancellationToken）
        app.RunAsync(ct).Wait(ct);
    }
}
