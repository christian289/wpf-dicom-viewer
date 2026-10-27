using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.UI.Controls;

/// <summary>
/// 컨트롤 DP 값으로부터 도메인 <see cref="ImageTransform"/>/<see cref="ImageViewport"/>를 만드는 도우미. 변환 수학은 도메인에만 있고 여기서는 값 검증과 생성만 한다.
/// Helper that builds the domain <see cref="ImageTransform"/>/<see cref="ImageViewport"/> from control DP values. The transform math lives only in the domain; this only validates and constructs.
/// </summary>
internal static class ViewerGeometry
{
    public static bool TryCreate(
        double viewportWidthDip,
        double viewportHeightDip,
        int imageWidthPx,
        int imageHeightPx,
        double zoomFactor,
        double panX,
        double panY,
        double rotationDegrees,
        bool flipHorizontal,
        bool flipVertical,
        out ImageTransform transform,
        out ImageViewport viewport)
    {
        transform = ImageTransform.Default;
        viewport = default;

        // 도메인 계산이 예외를 던질 값은 미리 걸러낸다 (렌더/입력 경로에서 예외 방지)
        // Filter values that would make the domain math throw (no exceptions on render/input paths)
        if (imageWidthPx <= 0 || imageHeightPx <= 0)
        {
            return false;
        }

        if (!IsPositiveFinite(viewportWidthDip) || !IsPositiveFinite(viewportHeightDip) || !IsPositiveFinite(zoomFactor))
        {
            return false;
        }

        if (!double.IsFinite(panX) || !double.IsFinite(panY) || !double.IsFinite(rotationDegrees))
        {
            return false;
        }

        transform = new ImageTransform(zoomFactor, panX, panY, rotationDegrees, flipHorizontal, flipVertical);
        viewport = new ImageViewport(viewportWidthDip, viewportHeightDip, imageWidthPx, imageHeightPx);
        return true;
    }

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0;
}