using DicomViewer.Application.DTOs;

namespace DicomViewer.Tests.Application;

public class PixelSpacingTests
{
    [Fact]
    public void Unknown_spacing_is_invalid_and_measures_in_pixels_not_millimetres()
    {
        var spacing = PixelSpacing.Unknown;

        Assert.False(spacing.IsValid);
        Assert.Equal(PixelSpacingUnit.Pixel, spacing.Unit);
        Assert.Equal(5.0, spacing.CalculateDistance(3, 4), 9);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, -0.5)]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    public void Non_positive_or_non_finite_axis_makes_spacing_unknown(double x, double y)
    {
        var spacing = new PixelSpacing(x, y);

        Assert.False(spacing.IsValid);
        Assert.Equal(PixelSpacingUnit.Pixel, spacing.Unit);
    }

    [Fact]
    public void Anisotropic_valid_spacing_scales_each_axis_before_combining()
    {
        // X(열)=0.5mm, Y(행)=2mm: 픽셀 차이 (2,1) → mm (1,2) → sqrt(5)
        // X (column) = 0.5 mm, Y (row) = 2 mm: pixel delta (2,1) → mm (1,2) → sqrt(5)
        var spacing = new PixelSpacing(X: 0.5, Y: 2.0);

        Assert.True(spacing.IsValid);
        Assert.Equal(PixelSpacingUnit.Millimeter, spacing.Unit);
        Assert.Equal(Math.Sqrt(5.0), spacing.CalculateDistance(2, 1), 9);
    }
}