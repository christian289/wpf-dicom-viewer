namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// 이미지 히스토그램 데이터
/// Image histogram data
/// </summary>
public sealed record Histogram(
    int[] Bins,
    int BinCount,
    int MinValue,
    int MaxValue,
    int PeakValue,
    int PeakIndex,
    long TotalPixels)
{
    /// <summary>
    /// 정규화된 빈 값 (0-1 범위)
    /// Normalized bin values (0-1 range)
    /// </summary>
    public double[] GetNormalizedBins()
    {
        if (PeakValue == 0) return new double[BinCount];

        var normalized = new double[BinCount];
        for (int i = 0; i < BinCount; i++)
        {
            normalized[i] = (double)Bins[i] / PeakValue;
        }
        return normalized;
    }

    /// <summary>
    /// 빈 히스토그램 생성
    /// Create empty histogram
    /// </summary>
    public static Histogram Empty(int binCount = 256) => new(
        new int[binCount],
        binCount,
        0,
        binCount - 1,
        0,
        0,
        0);
}
