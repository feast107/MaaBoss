using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MaaBoss.Core.Infrastructure;

namespace MaaBoss.Core.Services;

/// <summary>
/// Chrome DevTools Protocol (CDP) 客户端。
/// 用于连接已开启 remote-debugging-port 的 CEF/Chromium 应用（如 BOSS直聘）。
/// </summary>
public class CdpService : IAsyncDisposable
{
    private readonly HttpClient _http = new();
    private System.Net.WebSockets.ClientWebSocket? _ws;
    private string _debuggerUrl = "";
    private int _commandId = 0;

    /// <summary>BOSS直聘 默认调试端口</summary>
    public const int DefaultPort = 9222;

    /// <summary>
    /// 通过 PEB 注入启动 BOSS直聘 并开启调试端口。
    /// </summary>
    public static System.Diagnostics.Process LaunchBoss(string exePath, int port = DefaultPort)
        => BossLauncher.LaunchWithDebuggingPort(exePath, port);

    /// <summary>
    /// 连接到 CDP 端点（默认 localhost:9222）。
    /// 会自动获取第一个页面的 WebSocket 调试 URL 并连接。
    /// 注意：某些应用会检测 /json/list HTTP 请求并触发反调试。
    /// </summary>
    public async Task ConnectAsync(int port = DefaultPort, CancellationToken ct = default)
    {
        var listUrl = $"http://localhost:{port}/json/list";
        var json = await _http.GetStringAsync(listUrl, ct);
        using var doc = JsonDocument.Parse(json);
        string? wsUrl = null;
        foreach (var p in doc.RootElement.EnumerateArray())
        {
            if (p.GetProperty("type").GetString() == "page")
            {
                wsUrl = p.GetProperty("webSocketDebuggerUrl").GetString();
                break;
            }
        }

        if (string.IsNullOrEmpty(wsUrl))
            throw new InvalidOperationException("No page target found");

        await ConnectToWebSocketAsync(wsUrl, ct);
    }

    /// <summary>
    /// 直接通过 WebSocket URL 连接，跳过 HTTP 探测（绕过反调试）。
    /// 你需要通过其他方式（如内存扫描、日志文件）获取 wsUrl。
    /// </summary>
    public async Task ConnectToWebSocketAsync(string wsUrl, CancellationToken ct = default)
    {
        _debuggerUrl = wsUrl;
        _ws = new System.Net.WebSockets.ClientWebSocket();
        await _ws.ConnectAsync(new Uri(wsUrl), ct);
    }

    /// <summary>在当前页面执行 JavaScript，返回结果字符串。</summary>
    public async Task<string> EvaluateAsync(string expression, CancellationToken ct = default)
    {
        if (_ws == null) throw new InvalidOperationException("Not connected");

        var id = Interlocked.Increment(ref _commandId);
        var payload = new
        {
            id,
            method = "Runtime.evaluate",
            @params = new
            {
                expression,
                returnByValue = true,
                awaitPromise = true
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(new ArraySegment<byte>(bytes), System.Net.WebSockets.WebSocketMessageType.Text, true, ct);

        // 接收响应
        var buffer = new byte[8192];
        var sb = new StringBuilder();
        while (true)
        {
            var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (result.EndOfMessage) break;
        }

        return sb.ToString();
    }

    /// <summary>截取当前页面截图（Base64 PNG）。</summary>
    public async Task<string> ScreenshotAsync(CancellationToken ct = default)
    {
        if (_ws == null) throw new InvalidOperationException("Not connected");

        var id = Interlocked.Increment(ref _commandId);
        var payload = new
        {
            id,
            method = "Page.captureScreenshot",
            @params = new { format = "png" }
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(new ArraySegment<byte>(bytes), System.Net.WebSockets.WebSocketMessageType.Text, true, ct);

        var buffer = new byte[65536];
        var sb = new StringBuilder();
        while (true)
        {
            var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (result.EndOfMessage) break;
        }

        using var doc = JsonDocument.Parse(sb.ToString());
        var data = doc.RootElement.GetProperty("result").GetProperty("data").GetString();
        return data ?? "";
    }

    /// <summary>通过 DOM 选择器点击元素。</summary>
    public async Task ClickAsync(string selector, CancellationToken ct = default)
    {
        var js = $@"
            (function() {{
                var el = document.querySelector({JsonSerializer.Serialize(selector)});
                if (!el) throw new Error('Element not found: ' + {JsonSerializer.Serialize(selector)});
                el.click();
                return true;
            }})()
        ";
        var result = await EvaluateAsync(js, ct);
        if (result.Contains("\"exception\""))
            throw new InvalidOperationException($"Click failed: {result}");
    }

    /// <summary>通过 textContent 查找并点击元素。</summary>
    public async Task ClickByTextAsync(string text, CancellationToken ct = default)
    {
        var escapedText = JsonSerializer.Serialize(text);
        var js = $@"
            (function() {{
                var iter = document.createNodeIterator(document.body, NodeFilter.SHOW_TEXT, null, false);
                var node;
                while (node = iter.nextNode()) {{
                    if (node.textContent.includes({escapedText})) {{
                        node.parentElement.click();
                        return true;
                    }}
                }}
                throw new Error('Text not found: ' + {escapedText});
            }})()
        ";
        var result = await EvaluateAsync(js, ct);
        if (result.Contains("\"exception\""))
            throw new InvalidOperationException($"Click failed: {result}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_ws?.State == System.Net.WebSockets.WebSocketState.Open)
        {
            await _ws.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "dispose", CancellationToken.None);
        }
        _ws?.Dispose();
        _http.Dispose();
    }
}
