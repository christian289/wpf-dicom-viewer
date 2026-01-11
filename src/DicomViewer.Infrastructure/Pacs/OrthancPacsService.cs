using System.Net.Http.Json;
using System.Text.Json;

namespace DicomViewer.Infrastructure.Pacs;

/// <summary>
/// Orthanc PACS REST API 클라이언트
/// Orthanc PACS REST API client
/// </summary>
public sealed class OrthancPacsService(HttpClient httpClient, string baseUrl) : IPacsConnectionService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly string _baseUrl = baseUrl.TrimEnd('/');

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<DicomStudyDto>> QueryStudiesAsync(
        string? patientId = null,
        string? patientName = null,
        DateTime? studyDateFrom = null,
        DateTime? studyDateTo = null,
        CancellationToken cancellationToken = default)
    {
        // Orthanc REST API: GET /studies
        var response = await _httpClient.GetAsync($"{_baseUrl}/studies", cancellationToken);
        response.EnsureSuccessStatusCode();

        var studyIds = await response.Content.ReadFromJsonAsync<string[]>(JsonOptions, cancellationToken);
        if (studyIds is null) return [];

        var studies = new List<DicomStudyDto>();

        foreach (var studyId in studyIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var study = await GetStudyDetailsAsync(studyId, cancellationToken);
            if (study is null) continue;

            // 필터 적용
            // Apply filters
            if (!string.IsNullOrEmpty(patientId) &&
                !study.PatientId.Contains(patientId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(patientName) &&
                !study.PatientName.Contains(patientName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (studyDateFrom.HasValue && study.StudyDate < studyDateFrom.Value) continue;
            if (studyDateTo.HasValue && study.StudyDate > studyDateTo.Value) continue;

            studies.Add(study);
        }

        return studies;
    }

    public async Task<DicomStudyDto?> GetStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        // Orthanc에서는 studyInstanceUid로 직접 조회가 어려우므로
        // 모든 Study를 조회 후 필터링
        // Orthanc doesn't support direct lookup by studyInstanceUid
        // Query all studies and filter
        var studies = await QueryStudiesAsync(cancellationToken: cancellationToken);
        return studies.FirstOrDefault(s => s.StudyInstanceUid == studyInstanceUid);
    }

    public async Task<Stream?> GetInstanceStreamAsync(
        string sopInstanceUid,
        CancellationToken cancellationToken = default)
    {
        // Orthanc REST API: GET /instances/{id}/file
        // 먼저 인스턴스 ID를 찾아야 함
        // First need to find instance ID
        var instanceId = await FindInstanceIdAsync(sopInstanceUid, cancellationToken);
        if (instanceId is null) return null;

        var response = await _httpClient.GetAsync(
            $"{_baseUrl}/instances/{instanceId}/file",
            cancellationToken);

        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/system", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<DicomStudyDto?> GetStudyDetailsAsync(
        string orthancStudyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/studies/{orthancStudyId}",
                cancellationToken);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);

            var mainDicomTags = json.GetProperty("MainDicomTags");
            var patientMainDicomTags = json.GetProperty("PatientMainDicomTags");

            var studyInstanceUid = GetStringProperty(mainDicomTags, "StudyInstanceUID");
            if (string.IsNullOrEmpty(studyInstanceUid)) return null;

            var studyDateStr = GetStringProperty(mainDicomTags, "StudyDate");
            DateTime? studyDate = null;
            if (DateTime.TryParseExact(studyDateStr, "yyyyMMdd", null,
                System.Globalization.DateTimeStyles.None, out var parsed))
            {
                studyDate = parsed;
            }

            // Series 정보 조회
            // Get series information
            var seriesIds = json.GetProperty("Series").EnumerateArray()
                .Select(s => s.GetString())
                .Where(s => s is not null)
                .Cast<string>()
                .ToList();

            var seriesList = new List<DicomSeriesDto>();
            foreach (var seriesId in seriesIds)
            {
                var series = await GetSeriesDetailsAsync(seriesId, cancellationToken);
                if (series is not null)
                {
                    seriesList.Add(series);
                }
            }

            return new DicomStudyDto(
                StudyInstanceUid: studyInstanceUid,
                PatientId: GetStringProperty(patientMainDicomTags, "PatientID") ?? string.Empty,
                PatientName: GetStringProperty(patientMainDicomTags, "PatientName") ?? string.Empty,
                StudyDescription: GetStringProperty(mainDicomTags, "StudyDescription"),
                StudyDate: studyDate,
                AccessionNumber: GetStringProperty(mainDicomTags, "AccessionNumber"),
                Series: seriesList);
        }
        catch
        {
            return null;
        }
    }

    private async Task<DicomSeriesDto?> GetSeriesDetailsAsync(
        string orthancSeriesId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/series/{orthancSeriesId}",
                cancellationToken);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
            var mainDicomTags = json.GetProperty("MainDicomTags");

            var seriesInstanceUid = GetStringProperty(mainDicomTags, "SeriesInstanceUID");
            if (string.IsNullOrEmpty(seriesInstanceUid)) return null;

            // Instances 정보 조회
            // Get instances information
            var instanceIds = json.GetProperty("Instances").EnumerateArray()
                .Select(s => s.GetString())
                .Where(s => s is not null)
                .Cast<string>()
                .ToList();

            var instances = new List<DicomInstanceDto>();
            foreach (var instanceId in instanceIds)
            {
                var instance = await GetInstanceDetailsAsync(instanceId, cancellationToken);
                if (instance is not null)
                {
                    instances.Add(instance);
                }
            }

            return new DicomSeriesDto(
                SeriesInstanceUid: seriesInstanceUid,
                SeriesDescription: GetStringProperty(mainDicomTags, "SeriesDescription"),
                SeriesNumber: GetIntProperty(mainDicomTags, "SeriesNumber"),
                Modality: GetStringProperty(mainDicomTags, "Modality") ?? "Unknown",
                NumberOfInstances: instances.Count,
                Instances: instances.OrderBy(i => i.SliceLocation ?? i.InstanceNumber).ToList());
        }
        catch
        {
            return null;
        }
    }

    private async Task<DicomInstanceDto?> GetInstanceDetailsAsync(
        string orthancInstanceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/instances/{orthancInstanceId}",
                cancellationToken);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
            var mainDicomTags = json.GetProperty("MainDicomTags");

            var sopInstanceUid = GetStringProperty(mainDicomTags, "SOPInstanceUID");
            if (string.IsNullOrEmpty(sopInstanceUid)) return null;

            return new DicomInstanceDto(
                SopInstanceUid: sopInstanceUid,
                InstanceNumber: GetIntProperty(mainDicomTags, "InstanceNumber"),
                FilePath: $"{_baseUrl}/instances/{orthancInstanceId}/file",
                Rows: GetIntProperty(mainDicomTags, "Rows"),
                Columns: GetIntProperty(mainDicomTags, "Columns"),
                SliceLocation: GetDoubleProperty(mainDicomTags, "SliceLocation"));
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> FindInstanceIdAsync(
        string sopInstanceUid,
        CancellationToken cancellationToken)
    {
        // Orthanc REST API: POST /tools/lookup
        var content = new StringContent($"\"{sopInstanceUid}\"");
        var response = await _httpClient.PostAsync(
            $"{_baseUrl}/tools/lookup",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode) return null;

        var results = await response.Content.ReadFromJsonAsync<JsonElement[]>(JsonOptions, cancellationToken);
        if (results is null || results.Length == 0) return null;

        foreach (var result in results)
        {
            if (result.TryGetProperty("Type", out var typeElement) &&
                typeElement.GetString() == "Instance" &&
                result.TryGetProperty("ID", out var idElement))
            {
                return idElement.GetString();
            }
        }

        return null;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.GetString();
        }
        return null;
    }

    private static int GetIntProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32();
            }
            if (prop.ValueKind == JsonValueKind.String &&
                int.TryParse(prop.GetString(), out var value))
            {
                return value;
            }
        }
        return 0;
    }

    private static double? GetDoubleProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetDouble();
            }
            if (prop.ValueKind == JsonValueKind.String &&
                double.TryParse(prop.GetString(), out var value))
            {
                return value;
            }
        }
        return null;
    }
}
