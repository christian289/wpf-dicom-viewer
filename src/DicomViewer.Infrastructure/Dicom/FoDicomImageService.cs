using DicomViewer.Application.Exceptions;

namespace DicomViewer.Infrastructure.Dicom;

/// <summary>
/// fo-dicom 기반 DICOM 이미지 서비스
/// fo-dicom based DICOM image service
/// </summary>
/// <remarks>
/// 지원 범위: 단일 프레임, 비압축, Little Endian, MONOCHROME2, 1 샘플, 8/16비트 할당,
/// HighBit = BitsStored - 1, VOI LINEAR(또는 VOILUTFunction 없음).
/// Supported: single frame, uncompressed, little endian, MONOCHROME2, one sample, 8/16-bit allocated,
/// HighBit = BitsStored - 1, and VOI LINEAR (or no VOILUTFunction).
/// </remarks>
public sealed class FoDicomImageService : IDicomImageService
{
    /// <summary>
    /// 취소 확인 간격: 이 픽셀 수마다 한 번 확인한다 (행 단위가 아님)
    /// Cancellation checkpoint interval: checked once per this many pixels (not per row)
    /// </summary>
    private const int CancellationBlockSize = 65536;

    private const string InvalidDicomMessage =
        "DICOM 파일을 읽을 수 없습니다. / The file is not a readable DICOM file.";

    /// <summary>
    /// 헤더를 먼저 읽고 지원 프로필·크기를 검증한 뒤에만 프레임 바이트를 읽는다(ReadLargeOnDemand).
    /// 파일 스트림은 여기서 소유하며, 지연 바인딩된 픽셀 버퍼는 스트림을 닫기 전에 Extract가 모두 꺼낸다.
    /// Reads headers first and only extracts the frame bytes after the support profile and size are validated (ReadLargeOnDemand).
    /// The file stream is owned here; the lazily bound pixel buffer is fully extracted by Extract before the stream is closed.
    /// </summary>
    public async Task<LoadedDicomImage> LoadImageAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = OpenFileStream(filePath);
        var file = await OpenAsync(() => DicomFile.OpenAsync(stream, FileReadOption.ReadLargeOnDemand, 0)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return Extract(file, cancellationToken);
    }

    /// <summary>
    /// 호출자가 스트림 수명을 소유한다. 반환 전에 프레임을 모두 꺼내므로 반환 뒤에는 스트림을 읽지 않는다.
    /// The caller owns the stream lifetime. The frame is fully extracted before returning, so the stream is not read afterwards.
    /// </summary>
    public async Task<LoadedDicomImage> LoadImageFromStreamAsync(
        Stream dicomStream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = await OpenAsync(() => DicomFile.OpenAsync(dicomStream, FileReadOption.ReadLargeOnDemand, 0)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return Extract(file, cancellationToken);
    }

    /// <summary>
    /// 픽셀 값을 읽지 않고(ReadLargeOnDemand) 같은 지원 프로필로 헤더를 검증한 뒤 메타데이터를 반환한다.
    /// Reads headers lazily (ReadLargeOnDemand), validates them against the same support profile without
    /// materializing pixels, then returns the metadata. The file stream is owned here and closed on return.
    /// </summary>
    public async Task<DicomInstanceDto> ReadMetadataAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = OpenFileStream(filePath);
        var file = await OpenAsync(() => DicomFile.OpenAsync(stream, FileReadOption.ReadLargeOnDemand, 0)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            ReadSupportedHeader(file.Dataset, file.FileMetaInfo.TransferSyntax);
            return DicomInstanceMapper.Map(file.Dataset, filePath);
        }
        catch (DicomException)
        {
            throw new InvalidDataException(InvalidDicomMessage);
        }
    }

    public void ApplyWindowLevel(
        PixelData pixelData,
        WindowLevel windowLevel,
        Span<byte> destination,
        CancellationToken cancellationToken = default)
    {
        var pixelCount = ValidatePixelData(pixelData);
        ValidateWindowLevel(windowLevel);

        if (destination.Length < pixelCount)
        {
            throw new ArgumentException(
                $"Destination must hold at least {pixelCount} bytes.",
                nameof(destination));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 루프 밖에서 한 번만 계산하는 불변값 / Invariants computed once outside the loop
        var raw = pixelData.RawPixels;
        var sampleSize = pixelData.BitsAllocated / 8;
        var mask = (1 << pixelData.BitsStored) - 1;
        var signBit = 1 << (pixelData.BitsStored - 1);
        var isSigned = pixelData.IsSigned;
        var slope = pixelData.RescaleSlope;
        var intercept = pixelData.RescaleIntercept;
        var voi = VoiLinear.From(windowLevel);

        for (var blockStart = 0; blockStart < pixelCount;)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // pixelCount - blockStart는 항상 양수이므로 blockStart + 블록 크기가 int를 넘지 않는다
            // pixelCount - blockStart is always positive, so blockStart + block size cannot overflow int
            var blockEnd = blockStart + Math.Min(CancellationBlockSize, pixelCount - blockStart);
            for (var i = blockStart; i < blockEnd; i++)
            {
                var stored = ReadStoredValue(raw, i, sampleSize, mask, signBit, isSigned);
                destination[i] = voi.Apply(stored * slope + intercept);
            }

            blockStart = blockEnd;
        }
    }

    private static FileStream OpenFileStream(string filePath) =>
        new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);

    private static async Task<DicomFile> OpenAsync(Func<Task<DicomFile>> open)
    {
        try
        {
            return await open().ConfigureAwait(false);
        }
        catch (DicomException)
        {
            throw new InvalidDataException(InvalidDicomMessage);
        }
    }

    private static LoadedDicomImage Extract(DicomFile file, CancellationToken cancellationToken)
    {
        try
        {
            var dataset = file.Dataset;
            var header = ReadSupportedHeader(dataset, file.FileMetaInfo.TransferSyntax);

            // 크기를 검증한 뒤에만 프레임을 읽는다. .Data가 지연 버퍼를 스트림에서 소유 배열로 복사하므로 호출자가 스트림을 닫아도 안전하다.
            // The frame is read only after the size was validated. .Data copies the lazy buffer from the stream into an owned array,
            // so the stream may be closed afterwards.
            var frame = DicomPixelData.Create(dataset).GetFrame(0).Data;
            if (frame.Length < header.ByteCount)
            {
                throw new InvalidDataException("Pixel data is shorter than the declared frame size.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var pixels = new PixelData(
                Width: header.Columns,
                Height: header.Rows,
                BitsAllocated: header.BitsAllocated,
                BitsStored: header.BitsStored,
                IsSigned: header.IsSigned,
                RescaleSlope: header.RescaleSlope,
                RescaleIntercept: header.RescaleIntercept,
                RawPixels: frame);

            return new LoadedDicomImage(pixels, ReadPixelSpacing(dataset), ReadDefaultWindowLevel(dataset));
        }
        catch (DicomException)
        {
            throw new InvalidDataException(InvalidDicomMessage);
        }
    }

    /// <summary>
    /// 지원 프로필 검증. 실패는 명시적 미지원(UnsupportedDicomImageException) 또는 손상(InvalidDataException)이다.
    /// Support-profile validation. Failures are explicit unsupported (UnsupportedDicomImageException) or malformed (InvalidDataException).
    /// </summary>
    private static ImageHeader ReadSupportedHeader(DicomDataset dataset, DicomTransferSyntax transferSyntax)
    {
        if (transferSyntax.Equals(DicomTransferSyntax.ExplicitVRBigEndian))
        {
            throw new UnsupportedDicomImageException("Big endian transfer syntax is not supported.");
        }

        if (transferSyntax.IsEncapsulated)
        {
            throw new UnsupportedDicomImageException("Compressed transfer syntaxes are not supported.");
        }

        if (dataset.GetSingleValueOrDefault(DicomTag.NumberOfFrames, 1) != 1)
        {
            throw new UnsupportedDicomImageException("Multi-frame images are not supported.");
        }

        if (dataset.GetSingleValueOrDefault(DicomTag.SamplesPerPixel, (ushort)1) != 1)
        {
            throw new UnsupportedDicomImageException("Only single-sample images are supported.");
        }

        var photometric = dataset.GetSingleValueOrDefault(DicomTag.PhotometricInterpretation, string.Empty).Trim();
        if (photometric != "MONOCHROME2")
        {
            throw new UnsupportedDicomImageException("Only MONOCHROME2 images are supported.");
        }

        if (dataset.Contains(DicomTag.VOILUTSequence))
        {
            throw new UnsupportedDicomImageException("VOI LUT Sequence is not supported.");
        }

        if (dataset.Contains(DicomTag.VOILUTFunction) &&
            dataset.GetSingleValueOrDefault(DicomTag.VOILUTFunction, string.Empty).Trim() != "LINEAR")
        {
            throw new UnsupportedDicomImageException("Only the LINEAR VOI LUT function is supported.");
        }

        if (!dataset.Contains(DicomTag.PixelData) ||
            !dataset.Contains(DicomTag.PixelRepresentation) ||
            !dataset.Contains(DicomTag.BitsAllocated) ||
            !dataset.Contains(DicomTag.BitsStored) ||
            !dataset.Contains(DicomTag.HighBit))
        {
            throw new InvalidDataException("Required pixel attributes are missing.");
        }

        var rows = dataset.GetSingleValueOrDefault(DicomTag.Rows, (ushort)0);
        var columns = dataset.GetSingleValueOrDefault(DicomTag.Columns, (ushort)0);
        if (rows == 0 || columns == 0)
        {
            throw new InvalidDataException("Image dimensions must be positive.");
        }

        var bitsAllocated = dataset.GetSingleValueOrDefault(DicomTag.BitsAllocated, (ushort)0);
        if (bitsAllocated != 8 && bitsAllocated != 16)
        {
            throw new UnsupportedDicomImageException("Only 8 and 16 bits allocated are supported.");
        }

        var bitsStored = dataset.GetSingleValueOrDefault(DicomTag.BitsStored, (ushort)0);
        if (bitsStored < 1 || bitsStored > bitsAllocated)
        {
            throw new UnsupportedDicomImageException("BitsStored must be between 1 and BitsAllocated.");
        }

        // 시프트되거나 높은 비트에 저장된 레이아웃은 마스킹으로 조용히 해석하지 않고 거부한다
        // Shifted or high-bit layouts are rejected rather than silently reinterpreted by masking
        var highBit = dataset.GetSingleValueOrDefault(DicomTag.HighBit, (ushort)0);
        if (highBit != bitsStored - 1)
        {
            throw new UnsupportedDicomImageException("HighBit must equal BitsStored - 1.");
        }

        var pixelRepresentation = dataset.GetSingleValueOrDefault(DicomTag.PixelRepresentation, (ushort)0);
        if (pixelRepresentation > 1)
        {
            throw new UnsupportedDicomImageException("PixelRepresentation must be 0 or 1.");
        }

        var slope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);
        var intercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);
        if (!double.IsFinite(slope) || slope == 0 || !double.IsFinite(intercept))
        {
            throw new InvalidDataException("Rescale slope and intercept must be finite and the slope nonzero.");
        }

        // 바이트 수를 64비트로 계산하고 배열 한계를 넘는 크기는 프레임을 읽기 전에 거부한다
        // Byte count is computed in 64 bits; sizes beyond the array limit are rejected before reading the frame
        var byteCount = (long)rows * columns * (bitsAllocated / 8);
        if (byteCount > Array.MaxLength)
        {
            throw new UnsupportedDicomImageException("Image size exceeds the supported array limit.");
        }

        return new ImageHeader(
            Rows: rows,
            Columns: columns,
            BitsAllocated: bitsAllocated,
            BitsStored: bitsStored,
            IsSigned: pixelRepresentation == 1,
            RescaleSlope: slope,
            RescaleIntercept: intercept,
            ByteCount: (int)byteCount);
    }

    /// <summary>
    /// 유효하지 않은 간격은 Unknown으로 둔다 (단위 1mm 대체 없음)
    /// Invalid spacing becomes Unknown (no 1 mm substitution)
    /// </summary>
    private static PixelSpacing ReadPixelSpacing(DicomDataset dataset)
    {
        try
        {
            if (dataset.TryGetValues<double>(DicomTag.PixelSpacing, out var values) && values.Length == 2)
            {
                // DICOM PixelSpacing = [row(vertical), column(horizontal)]
                var spacing = new PixelSpacing(X: values[1], Y: values[0]);
                return spacing.IsValid ? spacing : PixelSpacing.Unknown;
            }

            return PixelSpacing.Unknown;
        }
        catch (DicomException)
        {
            return PixelSpacing.Unknown;
        }
    }

    /// <summary>
    /// 두 태그가 모두 없으면 기본값. 하나만 있거나 개수가 다르거나 값이 잘못되면 오류(조용한 기본값 금지).
    /// Both absent => default. Exactly one present, mismatched counts, or invalid values => error (no silent default).
    /// </summary>
    private static WindowLevel ReadDefaultWindowLevel(DicomDataset dataset)
    {
        var hasWidth = dataset.Contains(DicomTag.WindowWidth);
        var hasCenter = dataset.Contains(DicomTag.WindowCenter);

        if (!hasWidth && !hasCenter)
        {
            return WindowLevel.Default;
        }

        if (hasWidth != hasCenter)
        {
            throw new InvalidDataException("Window Center and Window Width must both be present.");
        }

        if (!dataset.TryGetValues<double>(DicomTag.WindowWidth, out var widths) ||
            !dataset.TryGetValues<double>(DicomTag.WindowCenter, out var centers) ||
            widths.Length == 0 ||
            widths.Length != centers.Length)
        {
            throw new InvalidDataException("Window Center and Window Width must be non-empty pairs of equal count.");
        }

        // 여러 쌍이면 첫 번째 쌍을 기본값으로 사용한다 / With several pairs, the first pair is the default
        var width = widths[0];
        var center = centers[0];
        if (!double.IsFinite(width) || width < 1 || !double.IsFinite(center))
        {
            throw new InvalidDataException("Window width must be at least 1 and both values finite.");
        }

        return new WindowLevel(width, center);
    }

    /// <summary>
    /// 저장 값 읽기: 리틀 엔디언, BitsStored 마스크, 부호 있는 값은 BitsStored-1 비트 기준 부호 확장
    /// Reads a stored sample: little endian, masked to BitsStored, sign-extended from bit BitsStored-1 when signed
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadStoredValue(byte[] raw, int index, int sampleSize, int mask, int signBit, bool isSigned)
    {
        var value = sampleSize == 2
            ? raw[index * 2] | (raw[index * 2 + 1] << 8)
            : raw[index];

        value &= mask;

        if (isSigned && (value & signBit) != 0)
        {
            value -= signBit << 1;
        }

        return value;
    }

    private static void ValidateWindowLevel(WindowLevel windowLevel)
    {
        if (!double.IsFinite(windowLevel.WindowCenter))
        {
            throw new ArgumentOutOfRangeException(nameof(windowLevel), "WindowCenter must be finite.");
        }

        if (!double.IsFinite(windowLevel.WindowWidth) || windowLevel.WindowWidth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(windowLevel), "WindowWidth must be finite and at least 1.");
        }
    }

    /// <summary>
    /// 픽셀 수를 64비트로 계산해 검증하고 int 픽셀 수를 반환한다.
    /// Validates the pixel count (computed in 64 bits) and returns it as an int.
    /// </summary>
    private static int ValidatePixelData(PixelData pixelData)
    {
        if (pixelData.Width <= 0 || pixelData.Height <= 0)
        {
            throw new ArgumentException("Width and height must be positive.", nameof(pixelData));
        }

        if (pixelData.BitsAllocated != 8 && pixelData.BitsAllocated != 16)
        {
            throw new ArgumentException("Only 8 and 16 bits allocated are supported.", nameof(pixelData));
        }

        if (pixelData.BitsStored < 1 || pixelData.BitsStored > pixelData.BitsAllocated)
        {
            throw new ArgumentException("BitsStored must be between 1 and BitsAllocated.", nameof(pixelData));
        }

        if (!double.IsFinite(pixelData.RescaleSlope) || !double.IsFinite(pixelData.RescaleIntercept))
        {
            throw new ArgumentException("Rescale slope and intercept must be finite.", nameof(pixelData));
        }

        var count = (long)pixelData.Width * pixelData.Height;
        var requiredBytes = count * (pixelData.BitsAllocated / 8);
        if (requiredBytes > Array.MaxLength)
        {
            throw new ArgumentException("Image size exceeds the supported array limit.", nameof(pixelData));
        }

        if (pixelData.RawPixels.Length < requiredBytes)
        {
            throw new ArgumentException("Raw pixel buffer is shorter than the pixel count requires.", nameof(pixelData));
        }

        return (int)count;
    }

    /// <summary>
    /// DICOM PS3.3 C.11.2.1.2.1 VOI LINEAR의 상수 경계. 출력은 연속값 y를 0 방향으로 절삭(truncate)하고 [0,255]로 고정한다.
    /// Constant bounds of DICOM PS3.3 C.11.2.1.2.1 VOI LINEAR. The continuous output y is truncated toward zero and clamped to [0,255].
    /// </summary>
    private readonly record struct VoiLinear(double Offset, double Low, double High, double Denominator)
    {
        public static VoiLinear From(WindowLevel windowLevel)
        {
            var offset = windowLevel.WindowCenter - 0.5;
            var half = (windowLevel.WindowWidth - 1.0) / 2.0;
            return new VoiLinear(offset, offset - half, offset + half, windowLevel.WindowWidth - 1.0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Apply(double x)
        {
            if (x <= Low) return 0;
            if (x > High) return 255;

            // Low == High (w == 1) makes this branch unreachable, so Denominator is never zero here
            // w == 1이면 Low == High이므로 이 분기는 도달하지 않아 Denominator는 0이 아니다
            var y = ((x - Offset) / Denominator + 0.5) * 255.0;
            return (byte)Math.Clamp(Math.Truncate(y), 0.0, 255.0);
        }
    }

    private sealed record ImageHeader(
        int Rows,
        int Columns,
        int BitsAllocated,
        int BitsStored,
        bool IsSigned,
        double RescaleSlope,
        double RescaleIntercept,
        int ByteCount);
}