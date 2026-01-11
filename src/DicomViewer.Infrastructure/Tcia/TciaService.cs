using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;

namespace DicomViewer.Infrastructure.Tcia;

/// <summary>
/// TCIA REST API 클라이언트
/// TCIA REST API client
/// </summary>
public sealed class TciaService(HttpClient httpClient) : ITciaService
{
    private const string BaseUrl = "https://services.cancerimagingarchive.net/nbia-api/services/v1";

    private readonly HttpClient _httpClient = httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<TciaCollectionDto>> GetCollectionsAsync(
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/getCollectionValues";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var collections = JsonSerializer.Deserialize<TciaCollectionResponse[]>(content, JsonOptions);

        return collections?.Select(c => new TciaCollectionDto(c.Collection)).ToList()
            ?? [];
    }

    public async Task<IReadOnlyList<TciaPatientDto>> GetPatientsAsync(
        string collection,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/getPatient?Collection={Uri.EscapeDataString(collection)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var patients = JsonSerializer.Deserialize<TciaPatientResponse[]>(content, JsonOptions);

        return patients?.Select(p => new TciaPatientDto(
            PatientId: p.PatientId,
            Collection: p.Collection,
            PatientName: p.PatientName)).ToList()
            ?? [];
    }

    public async Task<IReadOnlyList<TciaStudyDto>> GetPatientStudiesAsync(
        string collection,
        string patientId,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/getPatientStudy?Collection={Uri.EscapeDataString(collection)}&PatientID={Uri.EscapeDataString(patientId)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var studies = JsonSerializer.Deserialize<TciaStudyResponse[]>(content, JsonOptions);

        return studies?.Select(s => new TciaStudyDto(
            StudyInstanceUID: s.StudyInstanceUID,
            PatientId: s.PatientID,
            PatientName: s.PatientName,
            StudyDate: s.StudyDate,
            StudyDescription: s.StudyDescription,
            SeriesCount: s.SeriesCount)).ToList()
            ?? [];
    }

    public async Task<IReadOnlyList<TciaSeriesDto>> GetSeriesAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/getSeries?StudyInstanceUID={Uri.EscapeDataString(studyInstanceUid)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var seriesList = JsonSerializer.Deserialize<TciaSeriesResponse[]>(content, JsonOptions);

        return seriesList?.Select(s => new TciaSeriesDto(
            SeriesInstanceUID: s.SeriesInstanceUID,
            SeriesDescription: s.SeriesDescription,
            Modality: s.Modality,
            BodyPartExamined: s.BodyPartExamined,
            Manufacturer: s.Manufacturer,
            ImageCount: s.ImageCount,
            FileSize: s.FileSize)).ToList()
            ?? [];
    }

    // 다운로드 버퍼 크기 (81920 bytes = 80KB, .NET 권장 크기)
    // Download buffer size (81920 bytes = 80KB, .NET recommended size)
    private const int DownloadBufferSize = 81920;

    // 진행률 업데이트 간격 (밀리초)
    // Progress update interval (milliseconds)
    private const int ProgressUpdateIntervalMs = 100;

    public async Task<string> DownloadSeriesAsync(
        string seriesInstanceUid,
        string destinationFolder,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // ZIP 파일 다운로드
        // Download ZIP file
        var url = $"{BaseUrl}/getImage?SeriesInstanceUID={Uri.EscapeDataString(seriesInstanceUid)}";

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var zipPath = Path.Combine(destinationFolder, $"{seriesInstanceUid}.zip");
        var extractPath = Path.Combine(destinationFolder, seriesInstanceUid);

        // 임시 디렉토리 생성
        // Create temporary directory
        Directory.CreateDirectory(destinationFolder);

        // ZIP 다운로드 (최적화된 버퍼 크기 사용)
        // Download ZIP (using optimized buffer size)
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var lastProgressUpdate = stopwatch.ElapsedMilliseconds;
        var lastBytesForSpeed = 0L;
        var lastTimeForSpeed = 0L;
        var currentSpeed = 0.0;

        await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var fileStream = new FileStream(
            zipPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            DownloadBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            var buffer = new byte[DownloadBufferSize];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalRead += bytesRead;

                // 진행률 업데이트 (너무 자주 업데이트하지 않도록 제한)
                // Progress update (throttled to avoid too frequent updates)
                var currentTime = stopwatch.ElapsedMilliseconds;
                if (currentTime - lastProgressUpdate >= ProgressUpdateIntervalMs)
                {
                    // 다운로드 속도 계산 (이동 평균)
                    // Calculate download speed (moving average)
                    var timeDelta = (currentTime - lastTimeForSpeed) / 1000.0;
                    if (timeDelta > 0)
                    {
                        var bytesDelta = totalRead - lastBytesForSpeed;
                        var instantSpeed = bytesDelta / timeDelta;

                        // 지수 이동 평균으로 속도 스무딩
                        // Smooth speed with exponential moving average
                        currentSpeed = currentSpeed == 0
                            ? instantSpeed
                            : currentSpeed * 0.7 + instantSpeed * 0.3;

                        lastBytesForSpeed = totalRead;
                        lastTimeForSpeed = currentTime;
                    }

                    progress?.Report(new DownloadProgressInfo(
                        BytesDownloaded: totalRead,
                        TotalBytes: totalBytes,
                        SpeedBytesPerSecond: currentSpeed,
                        ElapsedTime: stopwatch.Elapsed));

                    lastProgressUpdate = currentTime;
                }
            }

            // 최종 진행률 업데이트
            // Final progress update
            progress?.Report(new DownloadProgressInfo(
                BytesDownloaded: totalRead,
                TotalBytes: totalBytes,
                SpeedBytesPerSecond: currentSpeed,
                ElapsedTime: stopwatch.Elapsed));
        }

        stopwatch.Stop();

        // ZIP 압축 해제
        // Extract ZIP
        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, true);
        }

        ZipFile.ExtractToDirectory(zipPath, extractPath);

        // ZIP 파일 삭제
        // Delete ZIP file
        File.Delete(zipPath);

        return extractPath;
    }

    public async Task<IReadOnlyList<string>> GetModalitiesAsync(
        string? collection = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/getModalityValues";
        if (!string.IsNullOrEmpty(collection))
        {
            url += $"?Collection={Uri.EscapeDataString(collection)}";
        }

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var modalities = await response.Content.ReadFromJsonAsync<TciaModalityResponse[]>(
            JsonOptions, cancellationToken);

        return modalities?.Select(m => m.Modality).Where(m => !string.IsNullOrEmpty(m)).ToList()!
            ?? [];
    }

    public async Task<IReadOnlyList<string>> GetBodyPartsAsync(
        string? collection = null,
        string? modality = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(collection))
        {
            queryParams.Add($"Collection={Uri.EscapeDataString(collection)}");
        }
        if (!string.IsNullOrEmpty(modality))
        {
            queryParams.Add($"Modality={Uri.EscapeDataString(modality)}");
        }

        var url = $"{BaseUrl}/getBodyPartValues";
        if (queryParams.Count > 0)
        {
            url += "?" + string.Join("&", queryParams);
        }

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var bodyParts = await response.Content.ReadFromJsonAsync<TciaBodyPartResponse[]>(
            JsonOptions, cancellationToken);

        return bodyParts?.Select(b => b.BodyPartExamined).Where(b => !string.IsNullOrEmpty(b)).ToList()!
            ?? [];
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/getCollectionValues", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #region Response DTOs

    private sealed record TciaCollectionResponse(string Collection);

    private sealed record TciaPatientResponse(
        string PatientId,
        string Collection,
        string? PatientName);

    private sealed record TciaStudyResponse(
        string StudyInstanceUID,
        string PatientID,
        string? PatientName,
        string? StudyDate,
        string? StudyDescription,
        int SeriesCount);

    private sealed record TciaSeriesResponse(
        string SeriesInstanceUID,
        string? SeriesDescription,
        string? Modality,
        string? BodyPartExamined,
        string? Manufacturer,
        int ImageCount,
        long FileSize);

    private sealed record TciaModalityResponse(string Modality);

    private sealed record TciaBodyPartResponse(string BodyPartExamined);

    #endregion
}
