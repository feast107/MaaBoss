using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MaaBoss.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace MaaBoss.Core.Tests;

/// <summary>
/// 消息按钮点击测试：在指定坐标范围内点击左侧边栏的"消息"按钮，
/// 然后截图并裁剪指定窗体区域。
/// </summary>
public class MessageButtonClickTests : IDisposable
{
    private readonly ControllerService _controller;
    private readonly ITestOutputHelper _output;

    // 消息按钮坐标范围（控制器坐标系）
    private const int MsgX1 = 13, MsgY1 = 161;
    private const int MsgX2 = 92, MsgY2 = 191;

    // 裁剪区域（控制器坐标系）
    private const int CropWinX1 = 114, CropWinY1 = 160;
    private const int CropWinX2 = 452, CropWinY2 = 713;

    public MessageButtonClickTests(ITestOutputHelper output)
    {
        _output = output;
        _controller = new ControllerService();
    }

    public void Dispose()
    {
        try { _controller.DisconnectAsync().Wait(TimeSpan.FromSeconds(3)); } catch { /* ignored */ }
    }

    [Fact]
    public async Task Click_MessageButton_And_Crop_Region()
    {
        // ---- 1. 连接 Win32 ----
        _output.WriteLine("[STEP 1] 正在连接 Win32 客户端...");
        var connectResult = await _controller.ConnectAsync();
        Assert.True(connectResult.Success,
            $"连接失败: {connectResult.ErrorMessage}。请确保 BOSS直聘 客户端已启动。");
        _output.WriteLine($"[STEP 1] 连接成功，分辨率: {connectResult.Resolution}");

        // ---- 2. 点击前截图 ----
        _output.WriteLine("[STEP 2] 点击前截图...");
        var screenshotDir = Path.Combine(AppContext.BaseDirectory, "test_screenshots");
        Directory.CreateDirectory(screenshotDir);
        var beforePath = Path.Combine(screenshotDir, $"msg_before_{DateTime.Now:HHmmss}.png");
        var scBefore = await _controller.ScreenshotAsync(beforePath);
        Assert.True(scBefore.Success, "截图失败");
        _output.WriteLine($"[STEP 2] 截图已保存: {beforePath}");

        // ---- 3. 计算点击坐标（范围中心点）----
        var targetX = (MsgX1 + MsgX2) / 2;  // 52
        var targetY = (MsgY1 + MsgY2) / 2;  // 176
        _output.WriteLine($"[STEP 3] 目标坐标: ({targetX}, {targetY})，范围: [{MsgX1},{MsgY1}] ~ [{MsgX2},{MsgY2}]");

        // ---- 4. 执行点击 ----
        _output.WriteLine("[STEP 4] 点击消息按钮...");
        var clickResult = await _controller.ClickAsync(targetX, targetY);
        Assert.True(clickResult.Success, "点击失败");
        _output.WriteLine($"[STEP 4] 点击成功: ({clickResult.X}, {clickResult.Y})");

        // ---- 5. 点击后截图 ----
        _output.WriteLine("[STEP 5] 点击后截图...");
        await Task.Delay(500); // 等待 UI 响应
        var afterPath = Path.Combine(screenshotDir, $"msg_after_{DateTime.Now:HHmmss}.png");
        var scAfter = await _controller.ScreenshotAsync(afterPath);
        Assert.True(scAfter.Success, "点击后截图失败");
        _output.WriteLine($"[STEP 5] 截图已保存: {afterPath}");

        // ---- 6. 裁剪指定区域（窗体坐标 → 图片像素）----
        _output.WriteLine("[STEP 6] 裁剪窗体区域...");
        var (ctrlW, ctrlH) = _controller.Resolution;
        _output.WriteLine($"[STEP 6] 控制器分辨率: {ctrlW}x{ctrlH}");

        var cropPath = await CropScreenshotAsync(afterPath, ctrlW, ctrlH,
            CropWinX1, CropWinY1, CropWinX2, CropWinY2);
        Assert.True(File.Exists(cropPath), "裁剪后的图片不存在");
        _output.WriteLine($"[STEP 6] 裁剪完成: {cropPath}");
    }

    /// <summary>
    /// 将窗体坐标裁剪区域转换为图片像素并裁剪保存。
    /// </summary>
    private async Task<string> CropScreenshotAsync(string screenshotPath,
        int ctrlW, int ctrlH,
        int winX1, int winY1, int winX2, int winY2)
    {
        var outputPath = Path.Combine(
            Path.GetDirectoryName(screenshotPath)!,
            $"crop_{Path.GetFileNameWithoutExtension(screenshotPath)}.png");

        // 把 Python 脚本写到临时文件，避免引号转义地狱
        var scriptPath = Path.Combine(Path.GetTempPath(), $"crop_{Guid.NewGuid():N}.py");
        var scriptContent = string.Join("\n", new[]
        {
            "from PIL import Image",
            $"img = Image.open(r'{screenshotPath.Replace("\\", "/")}')",
            $"ctrl_w, ctrl_h = {ctrlW}, {ctrlH}",
            "img_w, img_h = img.size",
            $"x1 = int({winX1} * img_w / ctrl_w)",
            $"y1 = int({winY1} * img_h / ctrl_h)",
            $"x2 = int({winX2} * img_w / ctrl_w)",
            $"y2 = int({winY2} * img_h / ctrl_h)",
            "crop = img.crop((x1, y1, x2, y2))",
            $"crop.save(r'{outputPath.Replace("\\", "/")}')",
            "print(f'Cropped: ' + str(img.size) + ' -> crop(' + str((x1,y1,x2,y2)) + ') = ' + str(crop.size))",
        });
        await File.WriteAllTextAsync(scriptPath, scriptContent);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = scriptPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var proc = Process.Start(psi)!;
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(stdout))
                _output.WriteLine($"[Crop] {stdout.Trim()}");
            if (!string.IsNullOrWhiteSpace(stderr))
                _output.WriteLine($"[Crop-ERR] {stderr.Trim()}");
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { }
        }

        return outputPath;
    }
}
