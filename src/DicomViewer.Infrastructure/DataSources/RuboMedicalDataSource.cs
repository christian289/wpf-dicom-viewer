using System.IO.Compression;
using DicomViewer.Application.DTOs;
using DicomViewer.Application.Interfaces;

namespace DicomViewer.Infrastructure.DataSources;

/// <summary>
/// Rubo Medical DICOM 샘플 데이터 소스
/// Rubo Medical DICOM sample data source
/// https://www.rubomedical.com/dicom_files/
/// </summary>
public sealed class RuboMedicalDataSource : IDicomDataSource
{
    private const string BaseUrl = "https://www.rubomedical.com/dicom_files/";

    private readonly HttpClient _httpClient;
    private readonly string _cacheFolder;

    // 알려진 샘플 목록 (Rubo Medical 웹사이트 기반)
    // Known sample list (based on Rubo Medical website)
    private static readonly DicomSampleDto[] KnownSamples =
    [
        new DicomSampleDto(
            "rubo-ct-head",
            "CT Head",
            "CT scan of the head",
            "CT",
            "HEAD",
            $"{BaseUrl}dicomdir_ct_head.zip",
            null,
            null),

        new DicomSampleDto(
            "rubo-mri-brain",
            "MRI Brain",
            "MRI scan of the brain",
            "MR",
            "BRAIN",
            $"{BaseUrl}dicomdir_mri_brain.zip",
            null,
            null),

        new DicomSampleDto(
            "rubo-cr-chest",
            "CR Chest",
            "Computed Radiography of the chest",
            "CR",
            "CHEST",
            $"{BaseUrl}dicomdir_cr_chest.zip",
            null,
            null),

        new DicomSampleDto(
            "rubo-us-abdomen",
            "US Abdomen",
            "Ultrasound of the abdomen",
            "US",
            "ABDOMEN",
            $"{BaseUrl}dicomdir_us_abdomen.zip",
            null,
            null),

        new DicomSampleDto(
            "rubo-xray",
            "X-Ray",
            "X-Ray sample images",
            "XA",
            null,
            $"{BaseUrl}dicomdir_xray.zip",
            null,
            null)
    ];

    public string Name => "Rubo Medical";

    public string Description => "Free DICOM sample files from Rubo Medical (rubomedical.com)";

    public RuboMedicalDataSource(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(10);

        _cacheFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DicomViewer",
            "RuboMedicalCache");

        Directory.CreateDirectory(_cacheFolder);
    }

    public Task<IReadOnlyList<DicomSampleDto>> GetSamplesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<DicomSampleDto>>(KnownSamples);
    }

    public async Task<string> DownloadSampleAsync(
        DicomSampleDto sample,
        string destinationFolder,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var extractPath = Path.Combine(destinationFolder, sample.Id);

        // 이미 다운로드된 경우 스킵
        // Skip if already downloaded
        if (Directory.Exists(extractPath) && Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories).Length > 0)
        {
            progress?.Report(new DownloadProgressInfo(1, 1, 0, TimeSpan.Zero));
            return extractPath;
        }

        var zipPath = Path.Combine(_cacheFolder, $"{sample.Id}.zip");
        var startTime = DateTime.UtcNow;

        // ZIP 다운로드
        // Download ZIP
        progress?.Report(new DownloadProgressInfo(0, 0, 0, TimeSpan.Zero));

        using (var response = await _httpClient.GetAsync(
            sample.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken))
        {
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var bytesRead = 0L;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            int read;

            while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                bytesRead += read;

                if (totalBytes > 0)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    var speed = elapsed.TotalSeconds > 0 ? bytesRead / elapsed.TotalSeconds : 0;
                    progress?.Report(new DownloadProgressInfo(bytesRead, totalBytes, speed, elapsed));
                }
            }
        }

        // ZIP 압축 해제
        // Extract ZIP
        var totalElapsed = DateTime.UtcNow - startTime;
        progress?.Report(new DownloadProgressInfo(1, 1, 0, totalElapsed));

        Directory.CreateDirectory(extractPath);
        ZipFile.ExtractToDirectory(zipPath, extractPath, true);

        // 임시 ZIP 파일 삭제
        // Delete temporary ZIP file
        try
        {
            File.Delete(zipPath);
        }
        catch
        {
            // 삭제 실패 무시
            // Ignore delete failure
        }

        progress?.Report(new DownloadProgressInfo(1, 1, 0, DateTime.UtcNow - startTime));

        return extractPath;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, BaseUrl);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
