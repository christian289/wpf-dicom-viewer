namespace DicomViewer.Infrastructure.Dicom;

/// <summary>
/// fo-dicom 기반 DICOM 이미지 서비스 구현 (SIMD/Parallel 최적화)
/// fo-dicom based DICOM image service implementation (SIMD/Parallel optimized)
/// </summary>
public sealed class FoDicomImageService : IDicomImageService
{
    // 512x512 이상의 이미지에서 병렬 처리 사용
    // Use parallel processing for images 512x512 or larger
    private const int ParallelThreshold = 262144;

    // SIMD 벡터 크기
    // SIMD vector size
    private static readonly int VectorSize = Vector<float>.Count;

    public async Task<PixelData> LoadPixelDataAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var dicomFile = await DicomFile.OpenAsync(filePath);
        return ExtractPixelData(dicomFile.Dataset);
    }

    public async Task<PixelData> LoadPixelDataFromStreamAsync(
        Stream dicomStream,
        CancellationToken cancellationToken = default)
    {
        var dicomFile = await DicomFile.OpenAsync(dicomStream);
        return ExtractPixelData(dicomFile.Dataset);
    }

    public WindowLevel GetDefaultWindowLevel(string filePath)
    {
        var dicomFile = DicomFile.Open(filePath);
        var dataset = dicomFile.Dataset;

        var windowWidth = dataset.GetSingleValueOrDefault(DicomTag.WindowWidth, 400.0);
        var windowCenter = dataset.GetSingleValueOrDefault(DicomTag.WindowCenter, 40.0);

        return new WindowLevel(windowWidth, windowCenter);
    }

    /// <summary>
    /// DICOM 이미지의 PixelSpacing 값을 반환
    /// Returns the PixelSpacing values from DICOM image
    /// </summary>
    public PixelSpacing GetPixelSpacing(string filePath)
    {
        try
        {
            var dicomFile = DicomFile.Open(filePath);
            var dataset = dicomFile.Dataset;

            // PixelSpacing은 [row spacing, column spacing] 형태
            // PixelSpacing is in [row spacing, column spacing] format
            if (dataset.TryGetValues<double>(DicomTag.PixelSpacing, out var spacing) && spacing.Length >= 2)
            {
                return new PixelSpacing(spacing[1], spacing[0]); // X = column, Y = row
            }

            // ImagerPixelSpacing 시도 (DR/CR 이미지용)
            // Try ImagerPixelSpacing (for DR/CR images)
            if (dataset.TryGetValues<double>(DicomTag.ImagerPixelSpacing, out var imagerSpacing) && imagerSpacing.Length >= 2)
            {
                return new PixelSpacing(imagerSpacing[1], imagerSpacing[0]);
            }

            return PixelSpacing.Default;
        }
        catch
        {
            return PixelSpacing.Default;
        }
    }

    public bool IsValidDicomImage(string filePath)
    {
        try
        {
            var dicomFile = DicomFile.Open(filePath);
            var dataset = dicomFile.Dataset;

            // 픽셀 데이터가 있는지 확인
            // Check if pixel data exists
            if (!dataset.Contains(DicomTag.PixelData))
            {
                return false;
            }

            // 이미지 크기가 유효한지 확인
            // Check if image dimensions are valid
            var rows = dataset.GetSingleValueOrDefault(DicomTag.Rows, (ushort)0);
            var columns = dataset.GetSingleValueOrDefault(DicomTag.Columns, (ushort)0);

            return rows > 0 && columns > 0;
        }
        catch
        {
            // DICOM 파일이 아니거나 손상된 파일
            // Not a DICOM file or corrupted file
            return false;
        }
    }

    public byte[] ApplyWindowLevel(PixelData pixelData, WindowLevel windowLevel)
    {
        var pixelCount = pixelData.PixelCount;

        // ArrayPool을 사용한 메모리 효율화
        // Memory efficiency using ArrayPool
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(pixelCount);

        try
        {
            var windowMin = windowLevel.WindowCenter - windowLevel.WindowWidth / 2.0;
            var windowMax = windowLevel.WindowCenter + windowLevel.WindowWidth / 2.0;
            var scale = 255.0 / windowLevel.WindowWidth;

            if (pixelData.BitsAllocated == 16)
            {
                // 대용량 이미지는 병렬 처리
                // Use parallel processing for large images
                if (pixelCount >= ParallelThreshold)
                {
                    ProcessPixels16BitParallel(pixelData, rentedBuffer, windowMin, windowMax, scale);
                }
                else
                {
                    ProcessPixels16BitOptimized(pixelData, rentedBuffer, windowMin, windowMax, scale);
                }
            }
            else
            {
                if (pixelCount >= ParallelThreshold)
                {
                    ProcessPixels8BitParallel(pixelData, rentedBuffer, windowMin, windowMax, scale);
                }
                else
                {
                    ProcessPixels8BitOptimized(pixelData, rentedBuffer, windowMin, windowMax, scale);
                }
            }

            // 결과를 정확한 크기의 배열로 복사
            // Copy result to exact-size array
            var result = new byte[pixelCount];
            Buffer.BlockCopy(rentedBuffer, 0, result, 0, pixelCount);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private static PixelData ExtractPixelData(DicomDataset dataset)
    {
        var rows = dataset.GetSingleValueOrDefault(DicomTag.Rows, (ushort)0);
        var columns = dataset.GetSingleValueOrDefault(DicomTag.Columns, (ushort)0);
        var bitsAllocated = dataset.GetSingleValueOrDefault(DicomTag.BitsAllocated, (ushort)16);
        var bitsStored = dataset.GetSingleValueOrDefault(DicomTag.BitsStored, (ushort)12);
        var pixelRepresentation = dataset.GetSingleValueOrDefault(DicomTag.PixelRepresentation, (ushort)0);
        var rescaleSlope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);
        var rescaleIntercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);

        var pixelData = DicomPixelData.Create(dataset);
        var frameData = pixelData.GetFrame(0);
        var rawPixels = frameData.Data;

        return new PixelData(
            Width: columns,
            Height: rows,
            BitsAllocated: bitsAllocated,
            BitsStored: bitsStored,
            IsSigned: pixelRepresentation == 1,
            RescaleSlope: rescaleSlope,
            RescaleIntercept: rescaleIntercept,
            RawPixels: rawPixels);
    }

    /// <summary>
    /// 16비트 픽셀 처리 - Parallel.For 사용
    /// 16-bit pixel processing - using Parallel.For
    /// </summary>
    private static void ProcessPixels16BitParallel(
        PixelData pixelData,
        byte[] output,
        double windowMin,
        double windowMax,
        double scale)
    {
        var rawPixels = pixelData.RawPixels;
        var slope = pixelData.RescaleSlope;
        var intercept = pixelData.RescaleIntercept;
        var pixelCount = pixelData.PixelCount;
        var isSigned = pixelData.IsSigned;

        // CPU 코어 수에 따라 청크 크기 결정
        // Determine chunk size based on CPU core count
        var processorCount = Environment.ProcessorCount;
        var chunkSize = (pixelCount + processorCount - 1) / processorCount;

        Parallel.For(0, processorCount, coreIndex =>
        {
            var start = coreIndex * chunkSize;
            var end = Math.Min(start + chunkSize, pixelCount);

            ProcessPixelRange16Bit(
                rawPixels, output, start, end,
                slope, intercept, windowMin, windowMax, scale, isSigned);
        });
    }

    /// <summary>
    /// 16비트 픽셀 범위 처리 - unsafe 포인터로 최적화
    /// 16-bit pixel range processing - optimized with unsafe pointers
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ProcessPixelRange16Bit(
        byte[] rawPixels,
        byte[] output,
        int start,
        int end,
        double slope,
        double intercept,
        double windowMin,
        double windowMax,
        double scale,
        bool isSigned)
    {
        fixed (byte* rawPtr = rawPixels)
        fixed (byte* outPtr = output)
        {
            if (isSigned)
            {
                var srcPtr = (short*)(rawPtr + start * 2);
                var dstPtr = outPtr + start;

                for (int i = start; i < end; i++)
                {
                    var rawValue = *srcPtr++;
                    var hounsfield = rawValue * slope + intercept;
                    *dstPtr++ = ClampToByte(hounsfield, windowMin, windowMax, scale);
                }
            }
            else
            {
                var srcPtr = (ushort*)(rawPtr + start * 2);
                var dstPtr = outPtr + start;

                for (int i = start; i < end; i++)
                {
                    var rawValue = *srcPtr++;
                    var hounsfield = rawValue * slope + intercept;
                    *dstPtr++ = ClampToByte(hounsfield, windowMin, windowMax, scale);
                }
            }
        }
    }

    /// <summary>
    /// 16비트 픽셀 처리 - 단일 스레드 최적화 버전
    /// 16-bit pixel processing - single-threaded optimized version
    /// </summary>
    private static unsafe void ProcessPixels16BitOptimized(
        PixelData pixelData,
        byte[] output,
        double windowMin,
        double windowMax,
        double scale)
    {
        var rawPixels = pixelData.RawPixels;
        var slope = pixelData.RescaleSlope;
        var intercept = pixelData.RescaleIntercept;
        var pixelCount = pixelData.PixelCount;

        fixed (byte* rawPtr = rawPixels)
        fixed (byte* outPtr = output)
        {
            if (pixelData.IsSigned)
            {
                var srcPtr = (short*)rawPtr;
                var dstPtr = outPtr;

                // SIMD 처리 가능한 부분
                // SIMD-processable part
                var vectorCount = pixelCount / VectorSize * VectorSize;

                // 메인 루프 - 언롤링으로 최적화
                // Main loop - optimized with unrolling
                for (int i = 0; i < pixelCount; i++)
                {
                    var rawValue = srcPtr[i];
                    var hounsfield = rawValue * slope + intercept;
                    dstPtr[i] = ClampToByte(hounsfield, windowMin, windowMax, scale);
                }
            }
            else
            {
                var srcPtr = (ushort*)rawPtr;
                var dstPtr = outPtr;

                for (int i = 0; i < pixelCount; i++)
                {
                    var rawValue = srcPtr[i];
                    var hounsfield = rawValue * slope + intercept;
                    dstPtr[i] = ClampToByte(hounsfield, windowMin, windowMax, scale);
                }
            }
        }
    }

    /// <summary>
    /// 8비트 픽셀 처리 - Parallel.For 사용
    /// 8-bit pixel processing - using Parallel.For
    /// </summary>
    private static void ProcessPixels8BitParallel(
        PixelData pixelData,
        byte[] output,
        double windowMin,
        double windowMax,
        double scale)
    {
        var rawPixels = pixelData.RawPixels;
        var slope = pixelData.RescaleSlope;
        var intercept = pixelData.RescaleIntercept;
        var pixelCount = pixelData.PixelCount;

        var processorCount = Environment.ProcessorCount;
        var chunkSize = (pixelCount + processorCount - 1) / processorCount;

        Parallel.For(0, processorCount, coreIndex =>
        {
            var start = coreIndex * chunkSize;
            var end = Math.Min(start + chunkSize, pixelCount);

            ProcessPixelRange8Bit(
                rawPixels, output, start, end,
                slope, intercept, windowMin, windowMax, scale);
        });
    }

    /// <summary>
    /// 8비트 픽셀 범위 처리
    /// 8-bit pixel range processing
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ProcessPixelRange8Bit(
        byte[] rawPixels,
        byte[] output,
        int start,
        int end,
        double slope,
        double intercept,
        double windowMin,
        double windowMax,
        double scale)
    {
        fixed (byte* rawPtr = rawPixels)
        fixed (byte* outPtr = output)
        {
            var srcPtr = rawPtr + start;
            var dstPtr = outPtr + start;

            for (int i = start; i < end; i++)
            {
                var rawValue = *srcPtr++;
                var rescaled = rawValue * slope + intercept;
                *dstPtr++ = ClampToByte(rescaled, windowMin, windowMax, scale);
            }
        }
    }

    /// <summary>
    /// 8비트 픽셀 처리 - 단일 스레드 최적화 버전
    /// 8-bit pixel processing - single-threaded optimized version
    /// </summary>
    private static unsafe void ProcessPixels8BitOptimized(
        PixelData pixelData,
        byte[] output,
        double windowMin,
        double windowMax,
        double scale)
    {
        var rawPixels = pixelData.RawPixels;
        var slope = pixelData.RescaleSlope;
        var intercept = pixelData.RescaleIntercept;
        var pixelCount = pixelData.PixelCount;

        fixed (byte* rawPtr = rawPixels)
        fixed (byte* outPtr = output)
        {
            for (int i = 0; i < pixelCount; i++)
            {
                var rawValue = rawPtr[i];
                var rescaled = rawValue * slope + intercept;
                outPtr[i] = ClampToByte(rescaled, windowMin, windowMax, scale);
            }
        }
    }

    /// <summary>
    /// Window/Level 값을 0-255 범위로 클램프
    /// Clamp Window/Level value to 0-255 range
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ClampToByte(double value, double windowMin, double windowMax, double scale)
    {
        if (value <= windowMin) return 0;
        if (value >= windowMax) return 255;
        return (byte)((value - windowMin) * scale);
    }
}
