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
    /// 算法：控件内坐标 → 相对控件百分比 → 乘算窗体/截图尺寸。
    /// </summary>
    private static bool TryMapPointerToTarget(Image img, Point pos, out int imgX, out int imgY, out int targetX, out int targetY)
    {
        imgX = imgY = targetX = targetY = -1;
        if (img.Source is not Bitmap bitmap) return false;

        var bounds = img.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return false;

        // 相对控件的百分比（0~1）
        double pctX = pos.X / bounds.Width;
        double pctY = pos.Y / bounds.Height;

        // 限制在 0~1 范围内
        pctX = double.Clamp(pctX, 0.0, 1.0);
        pctY = double.Clamp(pctY, 0.0, 1.0);

        var srcSize = bitmap.PixelSize;
        imgX = (int)(pctX * srcSize.Width);
        imgY = (int)(pctY * srcSize.Height);

        var vm = (DebugViewModel)img.DataContext!;
        var (targetW, targetH) = vm.GetControllerResolution();

        int w = targetW > 0 ? targetW : srcSize.Width;
        int h = targetH > 0 ? targetH : srcSize.Height;

        targetX = (int)(pctX * w);
        targetY = (int)(pctY * h);
        return true;
    }

    private void ScreenshotImage_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Image img) return;
        var pos = e.GetPosition(img);
        if (TryMapPointerToTarget(img, pos, out var ix, out var iy, out var tx, out var ty))
            ((DebugViewModel)DataContext!).UpdateCursorPosition(ix, iy, tx, ty);
        else
            ((DebugViewModel)DataContext!).UpdateCursorPosition(-1, -1, -1, -1);
    }

    private void ScreenshotImage_OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is DebugViewModel vm)
            vm.UpdateCursorPosition(-1, -1, -1, -1);
    }

    private void ScreenshotImage_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Image img) return;
        var pos = e.GetPosition(img);
        if (!TryMapPointerToTarget(img, pos, out var ix, out var iy, out var tx, out var ty))
            return;

        var vm = (DebugViewModel)DataContext!;
        vm.UpdateCursorPosition(ix, iy, tx, ty);
        _ = vm.ClickAtAsync(tx, ty);
    }
}
