using CommunityToolkit.Mvvm.Messaging.Messages;
using MaaBoss.Core.Models;

namespace MaaBoss.Core.Messaging;

/// <summary>
/// 连接状态变化消息。
/// </summary>
public class ConnectionStateChangedMessage : ValueChangedMessage<ConnectResult?>
{
    public ConnectionStateChangedMessage(ConnectResult? value) : base(value) { }
}

/// <summary>
/// 日志消息。
/// </summary>
public class LogMessage : ValueChangedMessage<string>
{
    public LogMessage(string value) : base(value) { }
}

/// <summary>
/// 截图完成消息。
/// </summary>
public class ScreenshotTakenMessage : ValueChangedMessage<string?>
{
    public ScreenshotTakenMessage(string? value) : base(value) { }
}
