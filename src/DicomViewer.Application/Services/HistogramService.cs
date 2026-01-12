using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Application.Services;

/// <summary>
/// 히스토그램 계산 서비스 (고성능 병렬 처리)
/// Histogram calculation service (high-performance parallel processing)
/// </summary>
public sealed class HistogramService
{
    private const int BinCount = 256;

    /// <summary>
    /// 8비트 픽셀 데이터에서 히스토그램 계산
    /// Calculate histogram from 8-bit pixel data
    /// </summary>
    public Histogram CalculateHistogram(byte[] pixels)
    {
        if (pixels is null || pixels.Length == 0)
            return Histogram.Empty();

        var bins = new int[BinCount];

        // 대용량 이미지는 병렬 처리
        // Use parallel processing for large images
        if (pixels.Length >= 262144) // 512x512
        {
            CalculateHistogramParallel(pixels, bins);
        }
        else
        {
            CalculateHistogramSequential(pixels, bins);
        }

        // 통계 계산
        // Calculate statistics
        int peakValue = 0;
        int peakIndex = 0;
        int minValue = BinCount - 1;
        int maxValue = 0;

        for (int i = 0; i < BinCount; i++)
        {
            if (bins[i] > 0)
            {
                if (i < minValue) minValue = i;
                if (i > maxValue) maxValue = i;

                if (bins[i] > peakValue)
                {
                    peakValue = bins[i];
                    peakIndex = i;
                }
            }
        }

        return new Histogram(
            bins,
            BinCount,
            minValue,
            maxValue,
            peakValue,
            peakIndex,
            pixels.Length);
    }

    /// <summary>
    /// 16비트 픽셀 데이터에서 히스토그램 계산 (256빈으로 축소)
    /// Calculate histogram from 16-bit pixel data (reduced to 256 bins)
    /// </summary>
    public Histogram CalculateHistogram(PixelData pixelData)
    {
        if (pixelData.BitsAllocated == 8)
        {
            return CalculateHistogram(pixelData.RawPixels);
        }

        // 16비트 데이터 처리
        // Process 16-bit data
        var bins = new int[BinCount];
        var rawPixels = pixelData.RawPixels;
        var pixelCount = pixelData.PixelCount;

        // ThreadLocal을 사용한 병렬 히스토그램 계산
        // Parallel histogram calculation using ThreadLocal
        var localBins = new ThreadLocal<int[]>(() => new int[65536], trackAllValues: true);

        Parallel.For(0, pixelCount, i =>
        {
            ushort value = pixelData.IsSigned
                ? (ushort)(BitConverter.ToInt16(rawPixels, i * 2) + 32768)
                : BitConverter.ToUInt16(rawPixels, i * 2);
            localBins.Value![value]++;
        });

        // 로컬 히스토그램 병합 및 256빈으로 축소
        // Merge local histograms and reduce to 256 bins
        foreach (var local in localBins.Values)
        {
            for (int i = 0; i < 65536; i++)
            {
                bins[i >> 8] += local[i]; // 256빈으로 축소
            }
        }

        localBins.Dispose();

        // 통계 계산
        // Calculate statistics
        int peakValue = 0;
        int peakIndex = 0;
        int minValue = BinCount - 1;
        int maxValue = 0;

        for (int i = 0; i < BinCount; i++)
        {
            if (bins[i] > 0)
            {
                if (i < minValue) minValue = i;
                if (i > maxValue) maxValue = i;

                if (bins[i] > peakValue)
                {
                    peakValue = bins[i];
                    peakIndex = i;
                }
            }
        }

        return new Histogram(
            bins,
            BinCount,
            minValue,
            maxValue,
            peakValue,
            peakIndex,
            pixelCount);
    }

    private static void CalculateHistogramParallel(byte[] pixels, int[] bins)
    {
        // ThreadLocal을 사용한 병렬 히스토그램
        // Parallel histogram using ThreadLocal
        var localBins = new ThreadLocal<int[]>(() => new int[BinCount], trackAllValues: true);

        Parallel.ForEach(
            Partitioner.Create(0, pixels.Length),
            range =>
            {
                var local = localBins.Value!;
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    local[pixels[i]]++;
                }
            });

        // 로컬 히스토그램 병합
        // Merge local histograms
        foreach (var local in localBins.Values)
        {
            for (int i = 0; i < BinCount; i++)
            {
                bins[i] += local[i];
            }
        }

        localBins.Dispose();
    }

    private static void CalculateHistogramSequential(byte[] pixels, int[] bins)
    {
        foreach (var pixel in pixels)
        {
            bins[pixel]++;
        }
    }
}
