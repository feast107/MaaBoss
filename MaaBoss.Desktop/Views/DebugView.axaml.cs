using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using MaaBoss.Desktop.ViewModels;

namespace MaaBoss.Desktop.Views;

public partial class DebugView : UserControl
{
    public DebugView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 将鼠标在 Image 控件上的位置换算为原始 Bitmap 像素坐标和目标窗体坐标。
    /// 算法：控件内坐标 → 相对控件百分比 → 乘算截图尺寸。
    /// 目标坐标直接采用截图像素坐标（与 Bitmap 1:1 对应），
    /// 避免 Controller.Resolution 与实际坐标系不一致导致的偏差。
    /// </summary>
    private static bool TryMapPointerToTarget(Image img, Point pos, out int imgX, out int imgY, out int targetX, out int targetY, out double pctX, out double pctY)
    {
        imgX = imgY = targetX = targetY = -1;
        pctX = pctY = 0;
        if (img.Source is not Bitmap bitmap) return false;

        var bounds = img.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return false;

        // 相对控件的百分比（0~1）
        pctX = pos.X / bounds.Width;
        pctY = pos.Y / bounds.Height;

        // 限制在 0~1 范围内
        pctX = double.Clamp(pctX, 0.0, 1.0);
        pctY = double.Clamp(pctY, 0.0, 1.0);

        var srcSize = bitmap.PixelSize;
        imgX = (int)(pctX * srcSize.Width);
        imgY = (int)(pctY * srcSize.Height);

        // 直接使用截图像素坐标作为目标坐标，确保与 Bitmap 1:1 对应
        targetX = imgX;
        targetY = imgY;
        return true;
    }

    private void ScreenshotImage_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Image img) return;
        var pos = e.GetPosition(img);
        if (TryMapPointerToTarget(img, pos, out var ix, out var iy, out var tx, out var ty, out var px, out var py))
            ((DebugViewModel)DataContext!).UpdateCursorPosition(ix, iy, tx, ty, px, py);
        else
            ((DebugViewModel)DataContext!).UpdateCursorPosition(-1, -1, -1, -1, 0, 0);
    }

    private void ScreenshotImage_OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is DebugViewModel vm)
            vm.UpdateCursorPosition(-1, -1, -1, -1, 0, 0);
    }

    private void ScreenshotImage_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Image img) return;
        var pos = e.GetPosition(img);
        if (!TryMapPointerToTarget(img, pos, out var ix, out var iy, out var tx, out var ty, out var px, out var py))
            return;

        var vm = (DebugViewModel)DataContext!;
        vm.UpdateCursorPosition(ix, iy, tx, ty, px, py);
        _ = vm.ClickAtAsync(tx, ty);
    }
}
