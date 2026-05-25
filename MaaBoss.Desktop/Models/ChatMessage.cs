namespace MaaBoss.Desktop.Models;

public class ChatMessage
{
    public string ContactName { get; set; } = "";
    public string CandidateInfo { get; set; } = "";
    public string Preview { get; set; } = "";
    public int UnreadCount { get; set; }
    public string Time { get; set; } = "";
    public string Type { get; set; } = "";
}
