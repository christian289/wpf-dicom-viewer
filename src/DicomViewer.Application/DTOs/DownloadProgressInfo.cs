namespace DicomViewer.Application.DTOs;

/// <summary>
/// 다운로드 진행 정보
/// Download progress information
/// </summary>
public sealed record DownloadProgressInfo(
    long BytesDownloaded,
    long TotalBytes,
    double SpeedBytesPerSecond,
    TimeSpan ElapsedTime)
{
    /// <summary>
    /// 진행률 (0.0 ~ 1.0)
    /// Progress percentage (0.0 ~ 1.0)
    /// </summary>
    public double Progress => TotalBytes > 0 ? (double)BytesDownloaded / TotalBytes : 0;

    /// <summary>
    /// 진행률 퍼센트 (0 ~ 100)
    /// Progress percentage (0 ~ 100)
    /// </summary>
    public double ProgressPercent => Progress * 100;

    /// <summary>
    /// 예상 남은 시간
    /// Estimated time remaining
    /// </summary>
    public TimeSpan EstimatedTimeRemaining
    {
        get
        {
            if (SpeedBytesPerSecond <= 0 || TotalBytes <= 0)
                return TimeSpan.Zero;

            var remainingBytes = TotalBytes - BytesDownloaded;
            var seconds = remainingBytes / SpeedBytesPerSecond;
            return TimeSpan.FromSeconds(seconds);
        }
    }

    /// <summary>
    /// 다운로드 속도 (포맷된 문자열)
    /// Download speed (formatted string)
    /// </summary>
    public string SpeedFormatted => FormatSpeed(SpeedBytesPerSecond);

    /// <summary>
    /// 다운로드된 크기 (포맷된 문자열)
    /// Downloaded size (formatted string)
    /// </summary>
    public string DownloadedFormatted => FormatSize(BytesDownloaded);

    /// <summary>
    /// 전체 크기 (포맷된 문자열)
    /// Total size (formatted string)
    /// </summary>
    public string TotalFormatted => FormatSize(TotalBytes);

    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB"];
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F1} {suffixes[suffixIndex]}";
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        string[] suffixes = ["B/s", "KB/s", "MB/s", "GB/s"];
        int suffixIndex = 0;
        double speed = bytesPerSecond;

        while (speed >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            speed /= 1024;
            suffixIndex++;
        }

        return $"{speed:F1} {suffixes[suffixIndex]}";
    }
}
