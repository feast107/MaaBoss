using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MaaFramework.Binding;
using MaaBoss.Desktop.Models;
using MaaBoss.Desktop.Services;

namespace MaaBoss.Desktop.Infrastructure.Mcp;

/// <summary>
/// MCP Server SSE 端点配置。
/// </summary>
public static class McpServerSetup
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IEndpointRouteBuilder MapMcpSseEndpoints(this IEndpointRouteBuilder app)
    {
        // MCP SSE 连接端点
        app.MapGet("/sse", async (HttpContext ctx, CancellationToken ct) =>
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");
            ctx.Response.Headers.Append("Cache-Control", "no-cache");
            ctx.Response.Headers.Append("Connection", "keep-alive");

            var sessionId = Guid.NewGuid().ToString("N");
            await ctx.Response.WriteAsync($"event: endpoint\n");
            await ctx.Response.WriteAsync($"data: /message?sessionId={sessionId}\n\n");
            await ctx.Response.Body.FlushAsync(ct);

            // 发送初始化消息
            await SendSseEventAsync(ctx, "initialize", new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                serverInfo = new { name = "maaboss", version = "0.1.0" }
            }, ct);

            // 保持连接
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                    await SendSseEventAsync(ctx, "ping", new { }, ct);
                }
            }
            catch (OperationCanceledException) { }
        });

        // MCP 消息接收端点
        app.MapPost("/message", async (HttpContext ctx, ControllerService ctrl, TaskService tasks, CancellationToken ct) =>
        {
            var request = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body, JsonOptions, ct);
            var method = request.GetProperty("method").GetString();
            var id = request.TryGetProperty("id", out var idProp) ? idProp.GetRawText() : "null";

            object? result = method switch
            {
                "tools/list" => new { tools = GetToolSchemas() },
                "tools/call" => await HandleToolCallAsync(request, ctrl, tasks, ct),
                _ => new { error = $"Unknown method: {method}" }
            };

            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { id, result }, JsonOptions), ct);
        });

        return app;
    }

    private static async Task SendSseEventAsync(HttpContext ctx, string eventName, object data, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await ctx.Response.WriteAsync($"event: {eventName}\n", ct);
        await ctx.Response.WriteAsync($"data: {json}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    private static List<object> GetToolSchemas()
    {
        return
        [
            new { name = "launch_app", description = "启动或连接到 Boss 直聘招聘端客户端", inputSchema = new { type = "object", properties = new { platform = new { type = "string", @enum = new[] { "win32", "adb" } }, adb_address = new { type = new[] { "string", "null" } }, wait_ready = new { type = "boolean", @default = true } }, required = new[] { "platform" } } },
            new { name = "browse_candidates", description = "浏览候选人列表", inputSchema = new { type = "object", properties = new { keyword = new { type = new[] { "string", "null" } }, experience = new { type = new[] { "string", "null" } }, education = new { type = new[] { "string", "null" } }, salary_expectation = new { type = new[] { "string", "null" } }, max_pages = new { type = "integer", @default = 3 }, list_type = new { type = "string", @default = "recommend" } } } },
            new { name = "swipe_candidates", description = "滑动浏览候选人", inputSchema = new { type = "object", properties = new { direction = new { type = "string", @default = "down" }, count = new { type = "integer", @default = 5 }, interval_ms = new { type = "integer", @default = 1500 } } } },
            new { name = "view_candidate_detail", description = "查看候选人详细简历", inputSchema = new { type = "object", properties = new { candidate_name = new { type = new[] { "string", "null" } }, extract_info = new { type = "boolean", @default = true } } } },
            new { name = "greet_candidate", description = "向候选人发起沟通", inputSchema = new { type = "object", properties = new { candidate_name = new { type = new[] { "string", "null" } }, greeting_msg = new { type = new[] { "string", "null" } }, confirm = new { type = "boolean", @default = true } } } },
            new { name = "batch_greet", description = "批量向候选人发起沟通", inputSchema = new { type = "object", properties = new { max_count = new { type = "integer", @default = 10 }, filter_new_only = new { type = "boolean", @default = true } } } },
            new { name = "get_unread_messages", description = "获取未读消息", inputSchema = new { type = "object", properties = new { filter_type = new { type = "string", @default = "all" } } } },
            new { name = "send_message", description = "向求职者发送回复", inputSchema = new { type = "object", properties = new { contact_name = new { type = "string" }, message = new { type = "string" }, wait_reply = new { type = "boolean", @default = false }, timeout_sec = new { type = "integer", @default = 30 } }, required = new[] { "contact_name", "message" } } },
            new { name = "mark_all_read", description = "标记所有消息已读", inputSchema = new { type = "object", properties = new { } } },
            new { name = "check_job_posts", description = "查看已发布职位状态", inputSchema = new { type = "object", properties = new { } } },
            new { name = "browse_applications", description = "浏览投递记录", inputSchema = new { type = "object", properties = new { job_title = new { type = new[] { "string", "null" } }, max_count = new { type = "integer", @default = 20 }, filter_unread = new { type = "boolean", @default = true } } } },
            new { name = "screenshot", description = "截取当前屏幕", inputSchema = new { type = "object", properties = new { save_path = new { type = new[] { "string", "null" } } } } },
            new { name = "reload_resources", description = "热重载资源", inputSchema = new { type = "object", properties = new { } } },
        ];
    }

    private static async Task<object> HandleToolCallAsync(JsonElement request, ControllerService ctrl, TaskService tasks, CancellationToken ct)
    {
        var args = request.GetProperty("params");
        var name = args.GetProperty("name").GetString()!;
        var arguments = args.TryGetProperty("arguments", out var a) ? a : new JsonElement();

        try
        {
            ToolResult result = name switch
            {
                "launch_app" => await tasks.LaunchAppAsync(
                    arguments.GetProperty("platform").GetString()!,
                    arguments.TryGetProperty("adb_address", out var adb) && adb.ValueKind != JsonValueKind.Null ? adb.GetString() : null,
                    arguments.TryGetProperty("wait_ready", out var wr) ? wr.GetBoolean() : true,
                    Win32ScreencapMethod.DXGI_DesktopDup_Window, ct),

                "browse_candidates" => await tasks.BrowseCandidatesAsync(
                    GetStringOrNull(arguments, "keyword"),
                    GetStringOrNull(arguments, "experience"),
                    GetStringOrNull(arguments, "education"),
                    GetStringOrNull(arguments, "salary_expectation"),
                    GetIntOrDefault(arguments, "max_pages", 3),
                    GetStringOrDefault(arguments, "list_type", "recommend"), ct),

                "swipe_candidates" => await tasks.SwipeCandidatesAsync(
                    GetStringOrDefault(arguments, "direction", "down"),
                    GetIntOrDefault(arguments, "count", 5),
                    GetIntOrDefault(arguments, "interval_ms", 1500), ct),

                "view_candidate_detail" => await tasks.ViewCandidateDetailAsync(
                    GetStringOrNull(arguments, "candidate_name"),
                    arguments.TryGetProperty("extract_info", out var ei) ? ei.GetBoolean() : true, ct),

                "greet_candidate" => await tasks.GreetCandidateAsync(
                    GetStringOrNull(arguments, "candidate_name"),
                    GetStringOrNull(arguments, "greeting_msg"),
                    arguments.TryGetProperty("confirm", out var c) ? c.GetBoolean() : true, ct),

                "batch_greet" => await tasks.BatchGreetAsync(
                    GetIntOrDefault(arguments, "max_count", 10),
                    arguments.TryGetProperty("filter_new_only", out var fn) ? fn.GetBoolean() : true, ct),

                "get_unread_messages" => await tasks.GetUnreadMessagesAsync(
                    GetStringOrDefault(arguments, "filter_type", "all"), ct),

                "send_message" => await tasks.SendMessageAsync(
                    arguments.GetProperty("contact_name").GetString()!,
                    arguments.GetProperty("message").GetString()!,
                    arguments.TryGetProperty("wait_reply", out var wr2) ? wr2.GetBoolean() : false,
                    GetIntOrDefault(arguments, "timeout_sec", 30), ct),

                "mark_all_read" => await tasks.MarkAllReadAsync(ct),
                "check_job_posts" => await tasks.CheckJobPostsAsync(ct),
                "browse_applications" => await tasks.BrowseApplicationsAsync(
                    GetStringOrNull(arguments, "job_title"),
                    GetIntOrDefault(arguments, "max_count", 20),
                    arguments.TryGetProperty("filter_unread", out var fu) ? fu.GetBoolean() : true, ct),

                "screenshot" => ToolResult.Ok(("path", (await ctrl.ScreenshotAsync(GetStringOrNull(arguments, "save_path"), ct)).Path ?? "")),
                "reload_resources" => await WrapReloadAsync(ctrl, ct),

                _ => ToolResult.Err("TOOL_NOT_FOUND", $"Unknown tool: {name}")
            };

            return ToMcpToolResult(result);
        }
        catch (Exception ex)
        {
            return ToMcpToolResult(ToolResult.Err("EXECUTION_ERROR", ex.Message));
        }
    }

    private static object ToMcpToolResult(ToolResult result)
    {
        var text = JsonSerializer.Serialize(result, JsonOptions);
        return new
        {
            content = new[] { new { type = "text", text } },
            isError = !result.Success
        };
    }

    private static async Task<ToolResult> WrapReloadAsync(ControllerService ctrl, CancellationToken ct)
    {
        await ctrl.ReloadResourcesAsync(ct);
        return ToolResult.Ok();
    }

    private static string? GetStringOrNull(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind == JsonValueKind.Null ? null : v.GetString();
    }

    private static string GetStringOrDefault(JsonElement el, string prop, string def)
    {
        if (!el.TryGetProperty(prop, out var v)) return def;
        return v.ValueKind == JsonValueKind.Null ? def : v.GetString() ?? def;
    }

    private static int GetIntOrDefault(JsonElement el, string prop, int def)
    {
        if (!el.TryGetProperty(prop, out var v)) return def;
        return v.ValueKind == JsonValueKind.Null ? def : v.GetInt32();
    }
}
