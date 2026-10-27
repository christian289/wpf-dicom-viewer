using DicomViewer.Domain.Enums;
using DicomViewer.Infrastructure.Dicom;

namespace DicomViewer.Infrastructure.Pacs;

/// <summary>
/// 로컬 폴더 기반 모의 PACS 서비스
/// Local folder based mock PACS service
/// </summary>
public sealed class LocalFolderPacsService(string basePath) : IPacsConnectionService
{
    private readonly string _basePath = basePath;

    public async Task<IReadOnlyList<DicomStudyDto>> QueryStudiesAsync(
        string? patientId = null,
        string? patientName = null,
        DateTime? studyDateFrom = null,
        DateTime? studyDateTo = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_basePath))
        {
            return [];
        }

        var dicomFiles = Directory.GetFiles(_basePath, "*.dcm", SearchOption.AllDirectories);
        var studyGroups = new Dictionary<string, List<DicomFile>>();

        foreach (var filePath in dicomFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var dicomFile = await DicomFile.OpenAsync(filePath);
                var studyUid = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, string.Empty);

                if (string.IsNullOrEmpty(studyUid)) continue;

                if (!studyGroups.TryGetValue(studyUid, out var files))
                {
                    files = [];
                    studyGroups[studyUid] = files;
                }
                files.Add(dicomFile);
            }
            catch
            {
                // 유효하지 않은 DICOM 파일 무시
                // Ignore invalid DICOM files
            }
        }

        var studies = new List<DicomStudyDto>();

        foreach (var (studyUid, files) in studyGroups)
        {
            var firstFile = files[0].Dataset;

            var studyPatientId = firstFile.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty);
            var studyPatientName = firstFile.GetSingleValueOrDefault(DicomTag.PatientName, string.Empty);

            // 필터 적용
            // Apply filters
            if (!string.IsNullOrEmpty(patientId) &&
                !studyPatientId.Contains(patientId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(patientName) &&
                !studyPatientName.Contains(patientName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var studyDateStr = firstFile.GetSingleValueOrDefault(DicomTag.StudyDate, string.Empty);
            DateTime? studyDate = null;
            if (DateTime.TryParseExact(studyDateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsed))
            {
                studyDate = parsed;

                if (studyDateFrom.HasValue && studyDate < studyDateFrom.Value) continue;
                if (studyDateTo.HasValue && studyDate > studyDateTo.Value) continue;
            }

            var seriesDtos = BuildSeriesFromFiles(files);

            studies.Add(new DicomStudyDto(
                StudyInstanceUid: studyUid,
                PatientId: studyPatientId,
                PatientName: studyPatientName,
                StudyDescription: firstFile.GetSingleValueOrDefault(DicomTag.StudyDescription, (string?)null),
                StudyDate: studyDate,
                AccessionNumber: firstFile.GetSingleValueOrDefault(DicomTag.AccessionNumber, (string?)null),
                Series: seriesDtos));
        }

        return studies;
    }

    public async Task<DicomStudyDto?> GetStudyAsync(
        string studyInstanceUid,
        CancellationToken cancellationToken = default)
    {
        var studies = await QueryStudiesAsync(cancellationToken: cancellationToken);
        return studies.FirstOrDefault(s => s.StudyInstanceUid == studyInstanceUid);
    }

    public Task<Stream?> GetInstanceStreamAsync(
        string sopInstanceUid,
        CancellationToken cancellationToken = default)
    {
        var dicomFiles = Directory.GetFiles(_basePath, "*.dcm", SearchOption.AllDirectories);

        foreach (var filePath in dicomFiles)
        {
            try
            {
                var dicomFile = DicomFile.Open(filePath);
                var instanceUid = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, string.Empty);

                if (instanceUid == sopInstanceUid)
                {
                    return Task.FromResult<Stream?>(File.OpenRead(filePath));
                }
            }
            catch
            {
                // 무시
                // Ignore
            }
        }

        return Task.FromResult<Stream?>(null);
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Directory.Exists(_basePath));
    }

    private static IReadOnlyList<DicomSeriesDto> BuildSeriesFromFiles(List<DicomFile> files)
    {
        var seriesGroups = files
            .GroupBy(f => f.Dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, string.Empty))
            .Where(g => !string.IsNullOrEmpty(g.Key));

        var seriesList = new List<DicomSeriesDto>();

        foreach (var group in seriesGroups)
        {
            var firstDataset = group.First().Dataset;
            var modalityStr = firstDataset.GetSingleValueOrDefault(DicomTag.Modality, "Unknown");

            var instances = group
                .Select(f => DicomInstanceMapper.Map(f.Dataset, f.File?.Name ?? string.Empty))
                .OrderBy(i => i.InstanceNumber)
                .ThenBy(i => i.SopInstanceUid, StringComparer.Ordinal)
                .ThenBy(i => i.FilePath, StringComparer.Ordinal)
                .ToList();

            seriesList.Add(new DicomSeriesDto(
                SeriesInstanceUid: group.Key,
                SeriesDescription: firstDataset.GetSingleValueOrDefault(DicomTag.SeriesDescription, (string?)null),
                SeriesNumber: firstDataset.GetSingleValueOrDefault(DicomTag.SeriesNumber, 0),
                Modality: modalityStr,
                NumberOfInstances: instances.Count,
                Instances: instances));
        }

        return seriesList;
    }
}