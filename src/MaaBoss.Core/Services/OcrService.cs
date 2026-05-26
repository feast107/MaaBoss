using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;

namespace MaaBoss.Core.Services;

/// <summary>
/// OCR 服务：基于 Sdcb.PaddleOCR 从图片中提取文字。
/// 首次使用时会自动从网络下载中文 V4 模型（约 10MB）。
/// </summary>
public class OcrService : IDisposable
{
    private readonly PaddleOcrAll _engine;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    public OcrService()
    {
        // 使用本地打包的模型，无需网络下载
        // 使用 ONNX Runtime 后端，彻底绕过 Paddle OneDNN 的兼容性问题
        _engine = new PaddleOcrAll(LocalFullModels.ChineseV4, PaddleDevice.Onnx());
    }

    /// <summary>
    /// 识别图片文件中的文字。
    /// </summary>
    public async Task<OcrResult> RecognizeAsync(string imagePath, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(imagePath))
                return new OcrResult(false, $"文件不存在: {imagePath}", []);

            return await Task.Run(() =>
            {
                using var mat = Cv2.ImRead(imagePath, ImreadModes.Color);
                if (mat.Empty())
                    return new OcrResult(false, "无法加载图片", []);

                var raw = _engine.Run(mat);
                if (raw == null)
                    return new OcrResult(false, "识别返回为空", []);

                var blocks = raw.Regions.Select(MakeBlock).ToArray();
                var fullText = string.Join("\n", blocks.Select(b => b.Text));
                return new OcrResult(true, null, blocks, fullText);
            }, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 识别图片字节数组中的文字。
    /// </summary>
    public async Task<OcrResult> RecognizeAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return await Task.Run(() =>
            {
                using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
                if (mat.Empty())
                    return new OcrResult(false, "无法解码图片", []);

                var raw = _engine.Run(mat);
                if (raw == null)
                    return new OcrResult(false, "识别返回为空", []);

                var blocks = raw.Regions.Select(MakeBlock).ToArray();
                var fullText = string.Join("\n", blocks.Select(b => b.Text));
                return new OcrResult(true, null, blocks, fullText);
            }, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static OcrTextBlock MakeBlock(PaddleOcrResultRegion r)
    {
        var rect = r.Rect.BoundingRect();
        var points = new[]
        {
            new OcrPoint(rect.Left, rect.Top),
            new OcrPoint(rect.Right, rect.Top),
            new OcrPoint(rect.Right, rect.Bottom),
            new OcrPoint(rect.Left, rect.Bottom),
        };
        return new OcrTextBlock(r.Text, r.Score, points);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _engine?.Dispose();
        _lock.Dispose();
    }
}

/// <summary>
/// OCR 识别结果。
/// </summary>
public record OcrResult(
    bool Success,
    string? ErrorMessage,
    IReadOnlyList<OcrTextBlock> Blocks,
    string? FullText = null);

/// <summary>
/// 单个文本块。
/// </summary>
public record OcrTextBlock(string Text, double Score, IReadOnlyList<OcrPoint> BoxPoints);

/// <summary>
/// 文本块顶点坐标。
/// </summary>
public record OcrPoint(double X, double Y);
