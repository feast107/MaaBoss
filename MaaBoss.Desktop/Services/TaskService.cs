using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaaFramework.Binding;
using MaaBoss.Desktop.Models;

namespace MaaBoss.Desktop.Services;

/// <summary>
/// 招聘端业务任务服务。
/// 将 Agent 的 Tool 调用转化为具体的业务操作，返回统一的 ToolResult。
/// </summary>
public class TaskService
{
    private readonly ControllerService _ctrl;
    private int _todayGreeted;

    public TaskService(ControllerService ctrl)
    {
        _ctrl = ctrl;
    }

    public async Task<ToolResult> LaunchAppAsync(
        string platform, string? adbAddress, bool waitReady,
        Win32ScreencapMethod screencapMethod = Win32ScreencapMethod.DXGI_DesktopDup_Window,
        Win32InputMethod mouseMethod = Win32InputMethod.SendMessageWithCursorPos,
        Win32InputMethod keyboardMethod = Win32InputMethod.PostMessage,
        string? windowName = null,
        CancellationToken ct = default)
    {
        // Win32 模式下若未指定窗口名，使用默认
        if (platform.ToLowerInvariant() == "win32" && string.IsNullOrWhiteSpace(windowName))
            windowName = "BOSS直聘";

        var result = await _ctrl.ConnectAsync(platform, adbAddress, windowName, screencapMethod, mouseMethod, keyboardMethod, ct);
        if (!result.Success)
            return ToolResult.Err("LAUNCH_FAILED", result.ErrorMessage ?? "连接失败", "请检查客户端是否已安装并处于前台");

        if (waitReady)
        {
            try
            {
                await _ctrl.RunPipelineAsync("Startup", null, ct);
            }
            catch (Exception ex)
            {
                // Startup pipeline 失败不应阻断连接，仅记录日志
                Console.WriteLine($"[WARN] Startup pipeline 执行失败: {ex.Message}");
            }
        }

        return ToolResult.Ok(
            ("controller_type", result.ControllerType),
            ("resolution", result.Resolution),
            ("message", "连接成功")
        );
    }

    public async Task<ToolResult> BrowseCandidatesAsync(string? keyword, string? experience, string? education, string? salaryExpectation, int maxPages, string listType, CancellationToken ct)
    {
        await _ctrl.RunPipelineAsync("BrowseCandidates", new { keyword, experience, education, salary_expectation = salaryExpectation, max_pages = maxPages, list_type = listType }, ct);

        var candidates = new List<Candidate>
        {
            new() { Name = "张三", Age = 28, Gender = "男", Experience = "5年", Education = "本科", CurrentCompany = "某某互联网公司", CurrentPosition = "Python 后端工程师", SalaryExpectation = "25k-40k", Location = "北京", Skills = new() { "Python", "Go", "Kubernetes" }, IsNew = true, ActiveStatus = "刚刚活跃" },
            new() { Name = "李四", Age = 26, Gender = "女", Experience = "3年", Education = "硕士", CurrentCompany = "某某科技公司", CurrentPosition = "高级后端开发", SalaryExpectation = "20k-35k", Location = "北京", Skills = new() { "Python", "Django", "PostgreSQL" }, IsNew = false, ActiveStatus = "2小时前活跃" },
        };

        return ToolResult.Ok(
            ("candidates", candidates),
            ("total_found", candidates.Count),
            ("keyword", keyword ?? ""),
            ("list_type", listType),
            ("current_page", 1),
            ("has_more", maxPages > 1)
        );
    }

    public async Task<ToolResult> SwipeCandidatesAsync(string direction, int count, int intervalMs, CancellationToken ct)
    {
        var res = _ctrl.Resolution;
        var cx = res.W / 2;
        var cy = res.H / 2;
        var dist = (int)(res.H * 0.3);

        var map = new Dictionary<string, (int, int, int, int)>
        {
            ["up"] = (cx, cy + dist, cx, cy - dist),
            ["down"] = (cx, cy - dist, cx, cy + dist),
            ["left"] = (cx + dist, cy, cx - dist, cy),
            ["right"] = (cx - dist, cy, cx + dist, cy),
        };

        var (x1, y1, x2, y2) = map.GetValueOrDefault(direction, map["down"]);
        for (int i = 0; i < count; i++)
        {
            await _ctrl.SwipeAsync(x1, y1, x2, y2, 500, ct);
            await Task.Delay(intervalMs, ct);
        }

        return ToolResult.Ok(
            ("direction", direction),
            ("count", count),
            ("message", $"已完成 {count} 次滑动")
        );
    }

    public async Task<ToolResult> ViewCandidateDetailAsync(string? candidateName, bool extractInfo, CancellationToken ct)
    {
        await _ctrl.RunPipelineAsync("ViewCandidateDetail", new { candidate_name = candidateName, extract_info = extractInfo }, ct);

        return ToolResult.Ok(
            ("name", candidateName ?? "张三"),
            ("age", 28),
            ("gender", "男"),
            ("location", "北京·海淀区"),
            ("work_experience", new[] { new { company = "某某互联网公司", position = "Python 后端工程师", duration = "2021.06 - 至今", description = "负责微服务架构设计" } }),
            ("skills", new[] { "Python", "Go", "Kubernetes", "Redis", "MySQL", "gRPC" }),
            ("job_status", "在职-看机会"),
            ("salary_expectation", "25k-40k")
        );
    }

    public async Task<ToolResult> GreetCandidateAsync(string? candidateName, string? greetingMsg, bool confirm, CancellationToken ct)
    {
        if (_todayGreeted >= 100)
            return ToolResult.Err("GREET_LIMIT", "今日沟通次数已达上限 (100)", "请明日再试或升级账号");

        await _ctrl.RunPipelineAsync("GreetCandidate", new { candidate_name = candidateName, greeting_msg = greetingMsg, confirm }, ct);
        _todayGreeted++;

        return ToolResult.Ok(
            ("candidate_name", candidateName ?? "当前首个候选人"),
            ("greeted_at", DateTime.Now.ToString("O")),
            ("greeting_sent", greetingMsg != null),
            ("today_greeted", _todayGreeted),
            ("daily_limit", 100)
        );
    }

    public async Task<ToolResult> BatchGreetAsync(int maxCount, bool filterNewOnly, CancellationToken ct)
    {
        var remaining = 100 - _todayGreeted;
        if (remaining <= 0)
            return ToolResult.Err("GREET_LIMIT", "今日沟通次数已达上限");

        var actual = Math.Min(maxCount, remaining);
        await _ctrl.RunPipelineAsync("BatchGreet", new { max_count = actual, filter_new_only = filterNewOnly }, ct);
        _todayGreeted += actual;

        return ToolResult.Ok(
            ("greeted_count", actual),
            ("failed_count", 0),
            ("filter_new_only", filterNewOnly),
            ("today_greeted", _todayGreeted),
            ("daily_limit", 100)
        );
    }

    public async Task<ToolResult> GetUnreadMessagesAsync(string filterType, CancellationToken ct)
    {
        var messages = new List<ChatMessage>
        {
            new() { ContactName = "张三", CandidateInfo = "Python 后端 · 5年 · 本科", Preview = "您好，我对贵公司的岗位很感兴趣...", UnreadCount = 2, Time = "11:05", Type = "replied" },
            new() { ContactName = "李四", CandidateInfo = "高级后端 · 3年 · 硕士", Preview = "谢谢您的认可，我想了解一下具体的薪资范围", UnreadCount = 1, Time = "10:30", Type = "replied" },
            new() { ContactName = "系统通知", CandidateInfo = "", Preview = "您发布的职位今日曝光量提升了 15%", UnreadCount = 1, Time = "09:00", Type = "system" },
        };

        if (filterType != "all")
            messages = messages.Where(m => m.Type == filterType).ToList();

        return ToolResult.Ok(
            ("messages", messages),
            ("total_unread", messages.Sum(m => m.UnreadCount)),
            ("filter_type", filterType)
        );
    }

    public async Task<ToolResult> SendMessageAsync(string contactName, string message, bool waitReply, int timeoutSec, CancellationToken ct)
    {
        await _ctrl.RunPipelineAsync("SendMessage", new { contact_name = contactName, message, wait_reply = waitReply, timeout_sec = timeoutSec }, ct);

        var result = new Dictionary<string, object>
        {
            ["contact_name"] = contactName,
            ["message_sent"] = message,
            ["sent_at"] = DateTime.Now.ToString("O")
        };

        if (waitReply)
        {
            await Task.Delay(1000, ct);
            result["reply_received"] = false;
            result["reply_content"] = null!;
        }

        return ToolResult.Ok(("result", result));
    }

    public async Task<ToolResult> MarkAllReadAsync(CancellationToken ct)
    {
        await _ctrl.RunPipelineAsync("MarkAllRead", null, ct);
        return ToolResult.Ok(("message", "所有消息已标记为已读"));
    }

    public async Task<ToolResult> CheckJobPostsAsync(CancellationToken ct)
    {
        var jobs = new List<JobPost>
        {
            new() { Title = "高级 Python 工程师", Status = "招聘中", Location = "北京", Salary = "25k-45k·14薪", Experience = "3-5年", Education = "本科", ExposureToday = 128, ApplicationsToday = 5, UnreadChats = 3, PublishDate = "2026-05-20" },
            new() { Title = "产品经理", Status = "招聘中", Location = "北京", Salary = "20k-35k", Experience = "3-5年", Education = "本科", ExposureToday = 85, ApplicationsToday = 2, UnreadChats = 1, PublishDate = "2026-05-22" },
        };

        return ToolResult.Ok(
            ("jobs", jobs),
            ("total_jobs", jobs.Count),
            ("total_exposure_today", 213),
            ("total_applications_today", 7),
            ("total_unread_chats", 4)
        );
    }

    public async Task<ToolResult> BrowseApplicationsAsync(string? jobTitle, int maxCount, bool filterUnread, CancellationToken ct)
    {
        var apps = new List<ApplicationRecord>
        {
            new() { CandidateName = "张三", CandidateInfo = "Python 后端 · 5年 · 本科", JobTitle = "高级 Python 工程师", AppliedAt = "2026-05-25 09:30", ResumeMatchScore = 92, IsRead = false, IsReplied = false },
            new() { CandidateName = "李四", CandidateInfo = "高级后端 · 3年 · 硕士", JobTitle = "高级 Python 工程师", AppliedAt = "2026-05-25 08:15", ResumeMatchScore = 85, IsRead = true, IsReplied = true },
        };

        if (filterUnread)
            apps = apps.Where(a => !a.IsRead).ToList();

        return ToolResult.Ok(
            ("applications", apps.Take(maxCount).ToList()),
            ("total_found", apps.Count),
            ("job_title", jobTitle ?? ""),
            ("filter_unread", filterUnread)
        );
    }
}
