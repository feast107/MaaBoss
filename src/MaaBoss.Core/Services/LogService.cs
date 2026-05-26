using System;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MaaBoss.Core.Services;

public partial class LogService : ObservableObject
{
    [ObservableProperty]
    public partial string LogText { get; set; } = "";

    private readonly StringBuilder _sb = new();
    private readonly int _maxLines = 500;

    public void Info(string message)
    {
        Append($"[INFO] {DateTime.Now:HH:mm:ss} {message}");
    }

    public void Debug(string message)
    {
        Append($"[DEBUG] {DateTime.Now:HH:mm:ss} {message}");
    }

    public void Warn(string message)
    {
        Append($"[WARN] {DateTime.Now:HH:mm:ss} {message}");
    }

    public void Error(string message)
    {
        Append($"[ERROR] {DateTime.Now:HH:mm:ss} {message}");
    }

    private void Append(string line)
    {
        _sb.AppendLine(line);
        var text = _sb.ToString();
        var lines = text.Split('\n');
        if (lines.Length > _maxLines)
        {
            _sb.Clear();
            foreach (var l in lines.Skip(lines.Length - _maxLines))
                _sb.AppendLine(l);
            text = _sb.ToString();
        }
        LogText = text;
    }

    public void Clear()
    {
        _sb.Clear();
        LogText = "";
    }
}
