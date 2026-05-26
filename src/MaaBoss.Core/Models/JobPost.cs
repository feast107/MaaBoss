namespace MaaBoss.Core.Models;

public class JobPost
{
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public string Location { get; set; } = "";
    public string Salary { get; set; } = "";
    public string Experience { get; set; } = "";
    public string Education { get; set; } = "";
    public int ExposureToday { get; set; }
    public int ApplicationsToday { get; set; }
    public int UnreadChats { get; set; }
    public string PublishDate { get; set; } = "";
}

public class ApplicationRecord
{
    public string CandidateName { get; set; } = "";
    public string CandidateInfo { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string AppliedAt { get; set; } = "";
    public int ResumeMatchScore { get; set; }
    public bool IsRead { get; set; }
    public bool IsReplied { get; set; }
}
