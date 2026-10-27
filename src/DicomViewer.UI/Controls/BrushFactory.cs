namespace DicomViewer.UI.Controls;

/// <summary>
/// 기본(폴백) Freeze 브러시 생성 도우미. 정적 필드 초기화 순서 문제 없이 DependencyProperty 기본값으로 사용할 수 있다.
/// Helper that creates default (fallback) frozen brushes; safe to use in static field initializers for DependencyProperty defaults.
/// </summary>
internal static class BrushFactory
{
    public static Brush Frozen(byte r, byte g, byte b, byte a = 255)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}