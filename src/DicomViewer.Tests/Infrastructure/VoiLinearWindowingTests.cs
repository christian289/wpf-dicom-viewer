using DicomViewer.Domain.ValueObjects;
using DicomViewer.Infrastructure.Dicom;

namespace DicomViewer.Tests.Infrastructure;

public class VoiLinearWindowingTests
{
    private static readonly FoDicomImageService Service = new();

    [Fact]
    public void Value_at_lower_edge_is_black()
    {
        // c=2048, w=4096: 하한 c-0.5-(w-1)/2 = 0 / lower edge c-0.5-(w-1)/2 = 0
        var output = Apply(Unsigned16(0), 16, 12, window: new WindowLevel(4096, 2048));

        Assert.Equal(0, output[0]);
    }

    [Fact]
    public void Value_at_upper_edge_is_white()
    {
        // 상한 c-0.5+(w-1)/2 = 4095 이며 선형 구간의 끝에서 255가 된다. 4096은 13비트로 저장해야 마스크되지 않는다.
        // Upper edge c-0.5+(w-1)/2 = 4095 maps to 255 at the end of the linear segment. 4096 needs 13 stored bits to survive masking.
        var output = Apply(Unsigned16(4095, 4096), 16, 13, window: new WindowLevel(4096, 2048));

        Assert.Equal(255, output[0]);
        Assert.Equal(255, output[1]);
    }

    [Fact]
    public void Width_one_is_a_threshold_at_center_minus_half()
    {
        // w=1: x <= c-0.5 이면 0, x > c-0.5 이면 255
        // w=1: x <= c-0.5 maps to 0, x > c-0.5 maps to 255
        var output = Apply(Unsigned16(2047, 2048), 16, 12, window: new WindowLevel(1, 2048));

        Assert.Equal(0, output[0]);
        Assert.Equal(255, output[1]);
    }

    [Fact]
    public void Signed_stored_value_is_sign_extended_from_bits_stored_not_from_sixteen_bits()
    {
        // BitsStored=12 signed: 0x0FFF 은 12비트에서 -1이므로 c=0,w=1 에서 0
        // BitsStored=12 signed: 0x0FFF is -1 in 12 bits, so with c=0,w=1 it maps to 0
        var raw = new byte[] { 0xFF, 0x0F };
        var output = Apply(raw, 16, 12, isSigned: true, window: new WindowLevel(1, 0));

        Assert.Equal(0, output[0]);
    }

    [Fact]
    public void Rescale_is_applied_before_windowing()
    {
        // 저장값 10 → 2*10-10 = 10. c=10, w=1: 10 > 9.5 이므로 255
        // Stored 10 → 2*10-10 = 10. With c=10, w=1: 10 > 9.5, so 255
        var output = Apply(Unsigned16(10), 16, 12, slope: 2, intercept: -10, window: new WindowLevel(1, 10));

        Assert.Equal(255, output[0]);
    }

    [Fact]
    public void Truncation_is_toward_zero_for_a_mid_range_value()
    {
        // c=100, w=200: x=0 → y = ((0-99.5)/199 + 0.5)*255 = 0.5*255 - 0.5*255... 직접 계산한 값 / exact value computed by hand
        // (0 - 99.5)/199 = -0.5 exactly, so y = (-0.5 + 0.5) * 255 = 0 ; x=100 → (0.5/199+0.5)*255 = 128.1…, truncated to 128
        var output = Apply(Unsigned16(0, 100), 16, 12, window: new WindowLevel(200, 100));

        Assert.Equal(0, output[0]);
        Assert.Equal(128, output[1]);
    }

    [Fact]
    public void Eight_bit_unsigned_samples_are_windowed_directly()
    {
        var raw = new byte[] { 0, 255 };
        var output = Apply(raw, 8, 8, window: new WindowLevel(255, 128));

        Assert.Equal(0, output[0]);
        Assert.Equal(255, output[1]);
    }

    [Fact]
    public void Destination_shorter_than_pixel_count_is_rejected()
    {
        var pixels = Pixels(Unsigned16(0, 0), 16, 12, slope: 1, intercept: 0);

        Assert.Throws<ArgumentException>(() =>
            Service.ApplyWindowLevel(pixels, new WindowLevel(400, 40), new byte[1]));
    }

    [Fact]
    public void Window_width_below_one_is_rejected()
    {
        var pixels = Pixels(Unsigned16(0), 16, 12, slope: 1, intercept: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Service.ApplyWindowLevel(pixels, new WindowLevel(0.5, 0), new byte[1]));
    }

    [Fact]
    public void Zero_width_image_is_rejected_before_writing()
    {
        var pixels = new PixelData(Width: 0, Height: 1, BitsAllocated: 16, BitsStored: 12, IsSigned: false,
            RescaleSlope: 1, RescaleIntercept: 0, RawPixels: []);
        var destination = new byte[1];

        Assert.Throws<ArgumentException>(() =>
            Service.ApplyWindowLevel(pixels, new WindowLevel(400, 40), destination));
        Assert.Equal(0, destination[0]);
    }

    [Fact]
    public void Dimensions_whose_product_exceeds_the_array_limit_are_rejected_without_overflow()
    {
        // 65535 x 65535 는 32비트 곱에서 넘치지만 64비트 검사로 거부된다 / overflows 32 bits but is rejected by 64-bit checking
        var pixels = new PixelData(Width: 65535, Height: 65535, BitsAllocated: 16, BitsStored: 12, IsSigned: false,
            RescaleSlope: 1, RescaleIntercept: 0, RawPixels: new byte[2]);

        Assert.Throws<ArgumentException>(() =>
            Service.ApplyWindowLevel(pixels, new WindowLevel(400, 40), new byte[1]));
    }

    [Fact]
    public void Pre_cancelled_token_throws_before_writing()
    {
        var pixels = Pixels(Unsigned16(0, 0), 16, 12, slope: 1, intercept: 0);
        var destination = new byte[2];
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            Service.ApplyWindowLevel(pixels, new WindowLevel(400, 40), destination, cts.Token));
        Assert.All(destination, b => Assert.Equal(0, b));
    }

    private static byte[] Apply(
        byte[] raw,
        int bitsAllocated,
        int bitsStored,
        bool isSigned = false,
        double slope = 1,
        double intercept = 0,
        WindowLevel? window = null)
    {
        var pixels = Pixels(raw, bitsAllocated, bitsStored, slope, intercept, isSigned);
        var output = new byte[pixels.PixelCount];
        Service.ApplyWindowLevel(pixels, window ?? WindowLevel.Default, output);
        return output;
    }

    private static PixelData Pixels(
        byte[] raw,
        int bitsAllocated,
        int bitsStored,
        double slope,
        double intercept,
        bool isSigned = false) =>
        new(Width: raw.Length * 8 / bitsAllocated, Height: 1, BitsAllocated: bitsAllocated, BitsStored: bitsStored,
            IsSigned: isSigned, RescaleSlope: slope, RescaleIntercept: intercept, RawPixels: raw);

    private static byte[] Unsigned16(params ushort[] values)
    {
        var bytes = new byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
        {
            bytes[i * 2] = (byte)(values[i] & 0xFF);
            bytes[i * 2 + 1] = (byte)(values[i] >> 8);
        }

        return bytes;
    }
}