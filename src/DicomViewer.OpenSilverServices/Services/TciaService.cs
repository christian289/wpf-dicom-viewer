using System.Net.Http;
using System.Text.Json;
using DicomViewer.OpenSilverServices.Interfaces;
using DicomViewer.OpenSilverServices.Models;

namespace DicomViewer.OpenSilverServices.Services;

/// <summary>
/// TCIA REST API 클라이언트 (OpenSilver/WebAssembly용)
/// TCIA REST API client (for OpenSilver/WebAssembly)
/// </summary>
public sealed class TciaService : ITciaService
{
    private const string BaseUrl = "https://services.cancerimagingarchive.net/nbia-api/services/v1";

    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public TciaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<TciaCollectionDto>> GetCollectionsAsync(
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/getCollectionValues";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<TciaCollectionDto>();
        }

        var collections = JsonSerializer.Deserialize<TciaCollectionResponse[]>(content, JsonOptions);

        return collections?.Select(c => new TciaCollectionDto(c.Collection)).ToList()
            ?? (IReadOnlyList<TciaCollectionDto>)Array.Empty<TciaCollectionDto>();
    }

    public async Task<IReadOnlyList<TciaPatientDto>> GetPatientsAsync(
        string collection,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/getPatient?Collection={Uri.EscapeDataString(collection)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<TciaPatientDto>();
        }

        var patients = JsonSerializer.Deserialize<TciaPatientResponse[]>(content, JsonOptions);

        return patients?.Select(p => new TciaPatientDto(
            p.PatientId,
            p.Collection,
            p.PatientName)).ToList()
            ?? (IReadOnlyList<TciaPatientDto>)Array.Empty<TciaPatientDto>();
    }

    public async Task<IReadOnlyList<TciaStudyDto>> GetPatientStudiesAsync(
        string collection,
        string patientId,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/getPatientStudy?Collection={Uri.EscapeDataString(collection)}&PatientID={Uri.EscapeDataString(patientId)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<TciaStudyDto>();
        }

        var studies = JsonSerializer.Deserialize<TciaStudyResponse[]>(content, JsonOptions);

        return studies?.Select(s => new TciaStudyDto(
            s.StudyInstanceUID,
            s.PatientID,
            s.PatientName,
            s.StudyDate,
            s.StudyDescription,
            s.SeriesCount)).ToList()
            ?? (IReadOnlyList<TciaStudyDto>)Array.Empty<TciaStudyDto>();
    }

    public async Task<IReadOnlyList<TciaSeriesDto>> GetSeriesAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/getSeries?StudyInstanceUID={Uri.EscapeDataString(studyInstanceUid)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<TciaSeriesDto>();
        }

        var seriesList = JsonSerializer.Deserialize<TciaSeriesResponse[]>(content, JsonOptions);

        return seriesList?.Select(s => new TciaSeriesDto(
            s.SeriesInstanceUID,
            s.SeriesDescription,
            s.Modality,
            s.BodyPartExamined,
            s.Manufacturer,
            s.ImageCount,
            s.FileSize)).ToList()
            ?? (IReadOnlyList<TciaSeriesDto>)Array.Empty<TciaSeriesDto>();
    }

    /// <summary>
    /// Series ZIP 파일을 메모리에 다운로드
    /// Download series ZIP file to memory
    /// </summary>
    public async Task<byte[]> DownloadSeriesAsync(
        string seriesInstanceUid,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/getImage?SeriesInstanceUID={Uri.EscapeDataString(seriesInstanceUid)}";

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;

        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var memoryStream = new MemoryStream();

        var buffer = new byte[81920]; // 80KB buffer
        long totalRead = 0;
        int bytesRead;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var lastProgressUpdate = 0L;
        var currentSpeed = 0.0;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await memoryStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalRead += bytesRead;

            // 진행률 업데이트 (100ms 간격)
            // Progress update (100ms interval)
            var currentTime = stopwatch.ElapsedMilliseconds;
            if (currentTime - lastProgressUpdate >= 100)
            {
                var timeDelta = (currentTime - lastProgressUpdate) / 1000.0;
                if (timeDelta > 0)
                {
                    var instantSpeed = bytesRead / timeDelta;
                    currentSpeed = currentSpeed == 0 ? instantSpeed : currentSpeed * 0.7 + instantSpeed * 0.3;
                }

                progress?.Report(new DownloadProgressInfo(
                    totalRead,
                    totalBytes,
                    currentSpeed,
                    stopwatch.Elapsed));

                lastProgressUpdate = currentTime;
            }
        }

        // 최종 진행률 업데이트
        // Final progress update
        progress?.Report(new DownloadProgressInfo(
            totalRead,
            totalBytes,
            currentSpeed,
            stopwatch.Elapsed));

        return memoryStream.ToArray();
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

    private sealed class TciaCollectionResponse
    {
        public string Collection { get; set; } = "";
    }

    private sealed class TciaPatientResponse
    {
        public string PatientId { get; set; } = "";
        public string Collection { get; set; } = "";
        public string? PatientName { get; set; }
    }

    private sealed class TciaStudyResponse
    {
        public string StudyInstanceUID { get; set; } = "";
        public string PatientID { get; set; } = "";
        public string? PatientName { get; set; }
        public string? StudyDate { get; set; }
        public string? StudyDescription { get; set; }
        public int SeriesCount { get; set; }
    }

    private sealed class TciaSeriesResponse
    {
        public string SeriesInstanceUID { get; set; } = "";
        public string? SeriesDescription { get; set; }
        public string? Modality { get; set; }
        public string? BodyPartExamined { get; set; }
        public string? Manufacturer { get; set; }
        public int ImageCount { get; set; }
        public long FileSize { get; set; }
    }

    #endregion
}
