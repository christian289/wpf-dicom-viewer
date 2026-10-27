using DicomViewer.Application.DTOs;
using DicomViewer.Application.Exceptions;
using DicomViewer.Infrastructure.Dicom;

using FellowOakDicom;
using FellowOakDicom.Imaging;

namespace DicomViewer.Tests.Infrastructure;

/// <summary>
/// 테스트 중 생성한 합성 DICOM을 실제 fo-dicom 판독 경로로 읽는다 (환자 데이터 없음)
/// Reads synthetic DICOM files generated during the test through the real fo-dicom path (no patient data)
/// </summary>
public class SyntheticDicomLoadTests
{
    private static readonly FoDicomImageService Service = new();

    [Fact]
    public async Task Multi_frame_image_is_rejected_explicitly_not_loaded_as_first_frame()
    {
        var dataset = SingleFrame(rows: 2, columns: 2);
        dataset.AddOrUpdate(DicomTag.NumberOfFrames, "2");
        await using var stream = await SaveAsync(dataset);

        await Assert.ThrowsAsync<UnsupportedDicomImageException>(() =>
            Service.LoadImageFromStreamAsync(stream));
    }

    [Fact]
    public async Task Multi_frame_image_is_rejected_by_metadata_read_without_loading_pixels()
    {
        var dataset = SingleFrame(rows: 2, columns: 2);
        dataset.AddOrUpdate(DicomTag.NumberOfFrames, "2");
        var path = await SaveToTempFileAsync(dataset);
        try
        {
            await Assert.ThrowsAsync<UnsupportedDicomImageException>(() => Service.ReadMetadataAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task High_bit_not_equal_to_bits_stored_minus_one_is_rejected_explicitly()
    {
        // BitsStored=12 인데 HighBit=15: 상위 비트에 저장된 레이아웃을 마스킹으로 추측하지 않는다
        // BitsStored=12 with HighBit=15: a high-bit layout is not guessed by masking
        var dataset = SingleFrame(rows: 2, columns: 2);
        dataset.AddOrUpdate(DicomTag.HighBit, (ushort)15);
        await using var stream = await SaveAsync(dataset);

        await Assert.ThrowsAsync<UnsupportedDicomImageException>(() =>
            Service.LoadImageFromStreamAsync(stream));
    }

    [Fact]
    public async Task Zero_rows_is_malformed_not_loaded_as_an_empty_image()
    {
        var dataset = SingleFrame(rows: 0, columns: 2);
        await using var stream = await SaveAsync(dataset);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Service.LoadImageFromStreamAsync(stream));
    }

    [Fact]
    public async Task Dimensions_beyond_the_array_limit_are_rejected_before_reading_the_frame()
    {
        // 65535 x 65535 x 2바이트는 배열 한계를 넘는다. 픽셀 배열은 짧게 두어 헤더 검사가 먼저 실행되는지 확인한다.
        // 65535 x 65535 x 2 bytes exceeds the array limit. The pixel array is kept short so the header check must run first.
        var dataset = SingleFrame(rows: 2, columns: 2);
        dataset.AddOrUpdate(DicomTag.Rows, (ushort)65535);
        dataset.AddOrUpdate(DicomTag.Columns, (ushort)65535);
        await using var stream = await SaveAsync(dataset);

        await Assert.ThrowsAsync<UnsupportedDicomImageException>(() =>
            Service.LoadImageFromStreamAsync(stream));
    }

    [Fact]
    public async Task Rejected_header_does_not_read_the_pixel_bytes_from_the_stream()
    {
        // 지원하지 않는 다중 프레임 헤더는 512x512 픽셀 바이트(512 KiB)를 스트림에서 읽기 전에 거부되어야 한다
        // An unsupported multi-frame header must be rejected before the 512 KiB of pixel bytes are read from the stream
        var dataset = SingleFrame(rows: 512, columns: 512);
        dataset.AddOrUpdate(DicomTag.NumberOfFrames, "2");
        await using var counting = new CountingStream(await SaveAsync(dataset));
        var pixelBytes = 512L * 512 * 2;

        await Assert.ThrowsAsync<UnsupportedDicomImageException>(() =>
            Service.LoadImageFromStreamAsync(counting));

        Assert.True(
            counting.BytesRead < pixelBytes,
            $"Read {counting.BytesRead} bytes but the pixel data alone is {pixelBytes} bytes.");
    }

    [Fact]
    public async Task Large_frame_loaded_from_a_stream_survives_the_stream_being_closed()
    {
        // 지연 로드된 픽셀 버퍼는 반환 전에 소유 배열로 복사되어야 한다 (호출자가 스트림을 바로 닫는다)
        // The lazily bound pixel buffer must be copied into an owned array before returning (the caller closes the stream right away)
        var dataset = SingleFrame(rows: 512, columns: 512);
        var pixels = new ushort[512 * 512];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (ushort)(i % 4096);
        }

        dataset.AddOrUpdate(DicomVR.OW, DicomTag.PixelData, pixels);
        var stream = await SaveAsync(dataset);

        var loaded = await Service.LoadImageFromStreamAsync(stream);
        await stream.DisposeAsync();

        Assert.Equal(512 * 512 * 2, loaded.Pixels.RawPixels.Length);
        for (var i = 0; i < pixels.Length; i++)
        {
            var stored = loaded.Pixels.RawPixels[i * 2] | (loaded.Pixels.RawPixels[(i * 2) + 1] << 8);
            Assert.Equal(pixels[i], stored);
        }
    }

    [Fact]
    public async Task Window_center_without_width_is_malformed_not_silently_defaulted()
    {
        var dataset = SingleFrame(rows: 2, columns: 2);
        dataset.AddOrUpdate(DicomTag.WindowCenter, "40");
        await using var stream = await SaveAsync(dataset);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Service.LoadImageFromStreamAsync(stream));
    }

    [Fact]
    public async Task Mismatched_window_center_and_width_counts_are_malformed()
    {
        var dataset = SingleFrame(rows: 2, columns: 2);
        dataset.AddOrUpdate(DicomTag.WindowCenter, "40\\30");
        dataset.AddOrUpdate(DicomTag.WindowWidth, "400");
        await using var stream = await SaveAsync(dataset);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Service.LoadImageFromStreamAsync(stream));
    }

    [Fact]
    public async Task Zero_pixel_spacing_loads_as_unknown_spacing_not_one_millimetre()
    {
        var dataset = SingleFrame(rows: 2, columns: 2);
        dataset.AddOrUpdate(DicomTag.PixelSpacing, "0", "0.5");
        await using var stream = await SaveAsync(dataset);

        var loaded = await Service.LoadImageFromStreamAsync(stream);

        Assert.Equal(PixelSpacing.Unknown, loaded.Spacing);
        Assert.Equal(PixelSpacingUnit.Pixel, loaded.Spacing.Unit);
    }

    [Fact]
    public async Task Valid_anisotropic_spacing_keeps_row_and_column_on_their_axes()
    {
        // DICOM PixelSpacing = [행(수직), 열(수평)] = [0.5, 0.25] → X=0.25, Y=0.5
        // DICOM PixelSpacing = [row (vertical), column (horizontal)] = [0.5, 0.25] → X=0.25, Y=0.5
        var dataset = SingleFrame(rows: 2, columns: 2);
        dataset.AddOrUpdate(DicomTag.PixelSpacing, "0.5", "0.25");
        await using var stream = await SaveAsync(dataset);

        var loaded = await Service.LoadImageFromStreamAsync(stream);

        Assert.Equal(0.25, loaded.Spacing.X, 9);
        Assert.Equal(0.5, loaded.Spacing.Y, 9);
    }

    [Fact]
    public async Task Metadata_read_returns_geometry_without_loading_pixel_data()
    {
        var dataset = SingleFrame(rows: 2, columns: 2);
        dataset.AddOrUpdate(DicomTag.SOPInstanceUID, "1.2.3.4.5");
        dataset.AddOrUpdate(DicomTag.InstanceNumber, "7");
        dataset.AddOrUpdate(DicomTag.ImagePositionPatient, "0", "0", "12.5");
        dataset.AddOrUpdate(DicomTag.ImageOrientationPatient, "1", "0", "0", "0", "1", "0");
        var path = await SaveToTempFileAsync(dataset);
        try
        {
            var metadata = await Service.ReadMetadataAsync(path);

            Assert.Equal("1.2.3.4.5", metadata.SopInstanceUid);
            Assert.Equal(7, metadata.InstanceNumber);
            Assert.Equal(new[] { 0.0, 0.0, 12.5 }, metadata.ImagePositionPatient);
            Assert.Equal(new[] { 1.0, 0.0, 0.0, 0.0, 1.0, 0.0 }, metadata.ImageOrientationPatient);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static DicomDataset SingleFrame(int rows, int columns)
    {
        var dataset = new DicomDataset();
        dataset.AddOrUpdate(DicomTag.SOPClassUID, DicomUID.CTImageStorage.UID);
        dataset.AddOrUpdate(DicomTag.SOPInstanceUID, "1.2.3.1");
        dataset.AddOrUpdate(DicomTag.PhotometricInterpretation, "MONOCHROME2");
        dataset.AddOrUpdate(DicomTag.SamplesPerPixel, (ushort)1);
        dataset.AddOrUpdate(DicomTag.Rows, (ushort)rows);
        dataset.AddOrUpdate(DicomTag.Columns, (ushort)columns);
        dataset.AddOrUpdate(DicomTag.BitsAllocated, (ushort)16);
        dataset.AddOrUpdate(DicomTag.BitsStored, (ushort)12);
        dataset.AddOrUpdate(DicomTag.HighBit, (ushort)11);
        dataset.AddOrUpdate(DicomTag.PixelRepresentation, (ushort)0);
        dataset.AddOrUpdate(DicomVR.OW, DicomTag.PixelData, new ushort[Math.Max(rows, 1) * Math.Max(columns, 1)]);
        return dataset;
    }

    private static async Task<MemoryStream> SaveAsync(DicomDataset dataset)
    {
        var file = new DicomFile(dataset);
        file.FileMetaInfo.TransferSyntax = DicomTransferSyntax.ExplicitVRLittleEndian;
        var stream = new MemoryStream();
        await file.SaveAsync(stream);
        stream.Position = 0;
        return stream;
    }

    private static async Task<string> SaveToTempFileAsync(DicomDataset dataset)
    {
        var file = new DicomFile(dataset);
        file.FileMetaInfo.TransferSyntax = DicomTransferSyntax.ExplicitVRLittleEndian;
        var path = Path.Combine(Path.GetTempPath(), $"viewer-synthetic-{Guid.NewGuid():N}.dcm");
        await file.SaveAsync(path);
        return path;
    }

    /// <summary>
    /// 내부 스트림에서 실제로 읽은 바이트 수를 센다 / Counts the bytes actually read from the inner stream
    /// </summary>
    private sealed class CountingStream(Stream inner) : Stream
    {
        public long BytesRead { get; private set; }

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

        public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

        public override async Task<int> ReadAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Count(await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken));

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private int Count(int read)
        {
            BytesRead += read;
            return read;
        }
    }
}