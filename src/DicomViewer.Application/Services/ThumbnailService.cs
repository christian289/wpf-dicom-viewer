using System.Buffers;
using DicomViewer.Application.DTOs;
using DicomViewer.Application.Interfaces;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Application.Services;

/// <summary>
/// 썸네일 생성 서비스 (병렬 처리)
/// Thumbnail generation service (parallel processing)
/// </summary>
public sealed class ThumbnailService
{
    private const int DefaultThumbnailSize = 64;

    private readonly IDicomImageService _imageService;

    public ThumbnailService(IDicomImageService imageService)
    {
        _imageService = imageService;
    }

    /// <summary>
    /// 단일 파일에서 썸네일 생성
    /// Generate thumbnail from single file
    /// </summary>
    public ThumbnailDto GenerateThumbnail(string filePath, int index, int targetSize = DefaultThumbnailSize)
    {
        try
        {
            var pixelData = _imageService.LoadPixelDataAsync(filePath).GetAwaiter().GetResult();
            var windowLevel = _imageService.GetDefaultWindowLevel(filePath);
            var pixels = _imageService.ApplyWindowLevel(pixelData, windowLevel);

            // 썸네일 다운스케일
            // Downsample for thumbnail
            var thumbnail = DownsampleImage(pixels, pixelData.Width, pixelData.Height, targetSize);

            return new ThumbnailDto(index, thumbnail, targetSize, targetSize);
        }
        catch
        {
            return ThumbnailDto.Empty(index, targetSize, targetSize);
        }
    }

    /// <summary>
    /// 여러 파일에서 썸네일 병렬 생성
    /// Generate thumbnails from multiple files in parallel
    /// </summary>
    public async Task<ThumbnailDto[]> GenerateThumbnailsAsync(
        IReadOnlyList<string> filePaths,
        int targetSize = DefaultThumbnailSize,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var thumbnails = new ThumbnailDto[filePaths.Count];
        var completed = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, filePaths.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            },
            async (index, ct) =>
            {
                thumbnails[index] = await Task.Run(() =>
                    GenerateThumbnail(filePaths[index], index, targetSize), ct);

                var current = Interlocked.Increment(ref completed);
                progress?.Report(current);
            });

        return thumbnails;
    }

    /// <summary>
    /// 이미지 다운스케일 (Nearest Neighbor)
    /// Downsample image (Nearest Neighbor)
    /// </summary>
    private static byte[] DownsampleImage(byte[] source, int srcWidth, int srcHeight, int targetSize)
    {
        var result = new byte[targetSize * targetSize];

        var scaleX = (double)srcWidth / targetSize;
        var scaleY = (double)srcHeight / targetSize;

        for (int y = 0; y < targetSize; y++)
        {
            var srcY = (int)(y * scaleY);
            if (srcY >= srcHeight) srcY = srcHeight - 1;

            for (int x = 0; x < targetSize; x++)
            {
                var srcX = (int)(x * scaleX);
                if (srcX >= srcWidth) srcX = srcWidth - 1;

                result[y * targetSize + x] = source[srcY * srcWidth + srcX];
            }
        }

        return result;
    }
}
