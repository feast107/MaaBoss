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
    /// 考虑 Uniform stretch 的缩放比例和居中偏移。
    /// </summary>
    private static bool TryMapPointerToTarget(Image img, Point pos, out int imgX, out int imgY, out int targetX, out int targetY)
    {
        imgX = imgY = targetX = targetY = -1;
        if (img.Source is not Bitmap bitmap) return false;

        var bounds = img.Bounds;
        var srcSize = bitmap.PixelSize;

        double scaleX = bounds.Width / srcSize.Width;
        double scaleY = bounds.Height / srcSize.Height;
        double scale = double.IsNaN(scaleX) || double.IsNaN(scaleY) ? 1.0 : System.Math.Min(scaleX, scaleY);

        double renderW = srcSize.Width * scale;
        double renderH = srcSize.Height * scale;
        double offsetX = (bounds.Width - renderW) / 2.0;
        double offsetY = (bounds.Height - renderH) / 2.0;

        // 检查鼠标是否在图片实际渲染区域内
        if (pos.X < offsetX || pos.X > offsetX + renderW ||
            pos.Y < offsetY || pos.Y > offsetY + renderH)
            return false;

        imgX = (int)((pos.X - offsetX) / scale);
        imgY = (int)((pos.Y - offsetY) / scale);

        var vm = (DebugViewModel)img.DataContext!;
        var (targetW, targetH) = vm.GetControllerResolution();

        targetX = targetW > 0 ? (int)(imgX * targetW / (double)srcSize.Width) : imgX;
        targetY = targetH > 0 ? (int)(imgY * targetH / (double)srcSize.Height) : imgY;
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
        if (!TryMapPointerToTarget(img, pos, out _, out _, out var tx, out var ty))
            return;

        var vm = (DebugViewModel)DataContext!;
        vm.UpdateCursorPosition((int)pos.X, (int)pos.Y, tx, ty);
        _ = vm.ClickAtAsync(tx, ty);
    }
}
