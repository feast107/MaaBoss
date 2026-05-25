using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MaaBoss.Desktop.Models;

/// <summary>
/// MCP Tool 统一返回结构。
/// </summary>
public class ToolResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Extra { get; set; }

    public static ToolResult Ok(params (string key, object value)[] extras)
    {
        var result = new ToolResult { Success = true };
        if (extras.Length > 0)
        {
            result.Extra = new Dictionary<string, object>();
            foreach (var (key, value) in extras)
                result.Extra[key] = value;
        }
        return result;
    }

    public static ToolResult Err(string code, string message, string? suggestion = null)
    {
        return new ToolResult
        {
            Success = false,
            ErrorCode = code,
            ErrorMessage = message,
            Suggestion = suggestion,
        };
    }
}
