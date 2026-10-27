using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Tests.Domain;

public class ImageViewportTransformTests
{
    private const double Epsilon = 1e-9;

    private static readonly ImageViewport Viewport = new(200, 100, 100, 100);

    [Fact]
    public void ImageToViewport_then_ViewportToImage_roundtrips_for_combined_transform()
    {
        var transform = new ImageTransform(
            ZoomFactor: 2.0,
            PanX: 5,
            PanY: -3,
            Rotation: 30,
            FlipHorizontal: true,
            FlipVertical: false);

        var source = new Point2D(17.5, 81.25);

        var viewport = transform.ImageToViewport(source, Viewport);
        var back = transform.ViewportToImage(viewport, Viewport);

        Assert.Equal(source.X, back.X, 9);
        Assert.Equal(source.Y, back.Y, 9);
    }

    [Fact]
    public void Viewport_point_outside_image_is_reported_outside_and_clamped_inside()
    {
        var transform = new ImageTransform();
        var farOutside = new Point2D(-500, 10_000);

        Assert.False(transform.TryViewportToImage(farOutside, Viewport, out _));

        var clamped = transform.ViewportToImageClamped(farOutside, Viewport);
        Assert.InRange(clamped.X, 0, Viewport.ImageWidthPx);
        Assert.InRange(clamped.Y, 0, Viewport.ImageHeightPx);
    }

    [Fact]
    public void Point_inside_image_is_inside_and_maps_back_to_its_image_coordinate()
    {
        var transform = new ImageTransform(Rotation: 90);
        var source = new Point2D(10, 20);
        var viewportPoint = transform.ImageToViewport(source, Viewport);

        var inside = transform.TryViewportToImage(viewportPoint, Viewport, out var mapped);

        Assert.True(inside);
        Assert.Equal(source.X, mapped.X, 9);
        Assert.Equal(source.Y, mapped.Y, 9);
    }

    [Fact]
    public void Fit_scale_uses_the_limiting_axis()
    {
        // 폭은 2 DIP/px, 높이는 1 DIP/px로 맞아야 하므로 작은 값 1이 선택된다
        // Width allows 2 DIP/px and height allows 1 DIP/px, so the smaller value 1 is chosen
        Assert.Equal(1.0, Viewport.FitScaleDipPerPixel, Epsilon);
    }

    [Fact]
    public void Pixel_distance_is_invariant_under_viewport_dpi_scaling()
    {
        var start = new Point2D(10, 10);
        var end = new Point2D(40, 70);
        var pixelDistance = Math.Sqrt(30 * 30 + 60 * 60); // 픽셀 차이 (30,60) / pixel delta (30,60)

        var small = new ImageViewport(200, 100, 100, 100);
        var large = new ImageViewport(300, 150, 100, 100); // 1.5배 DPI 상당 / equivalent of 1.5x DPI
        var transform = new ImageTransform(ZoomFactor: 1.25, Rotation: 17);

        var distanceSmall = Measure(transform, small, start, end);
        var distanceLarge = Measure(transform, large, start, end);

        Assert.Equal(pixelDistance, distanceSmall, 6);
        Assert.Equal(pixelDistance, distanceLarge, 6);
    }

    [Fact]
    public void Flip_is_applied_before_rotation_and_pan_is_applied_last()
    {
        // 수평 반전 후 회전 없이 이동하면, 영상 좌상단 (0,0)은 뷰포트 우측 끝 쪽으로 간다
        // With horizontal flip and no rotation, the image top-left (0,0) maps to the right side of the viewport
        var transform = new ImageTransform(FlipHorizontal: true, PanX: 4, PanY: 6);
        var mapped = transform.ImageToViewport(new Point2D(0, 0), Viewport);

        Assert.Equal(200.0 / 2 + 50 + 4, mapped.X, 9);
        Assert.Equal(100.0 / 2 - 50 + 6, mapped.Y, 9);
    }

    [Fact]
    public void Non_finite_or_nonpositive_zoom_is_rejected()
    {
        var transform = new ImageTransform(ZoomFactor: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => transform.ImageToViewport(Point2D.Zero, Viewport));
    }

    [Fact]
    public void Nonpositive_viewport_or_image_size_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ImageViewport(0, 100, 100, 100).FitScaleDipPerPixel);
    }

    private static double Measure(ImageTransform transform, ImageViewport viewport, Point2D a, Point2D b)
    {
        var va = transform.ImageToViewport(a, viewport);
        var vb = transform.ImageToViewport(b, viewport);
        var dipDistance = va.DistanceTo(vb);
        return dipDistance / transform.DipPerImagePixel(viewport);
    }
}