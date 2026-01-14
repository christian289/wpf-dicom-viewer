namespace DicomViewer.OpenSilverServices.Models;

/// <summary>
/// TCIA Collection 전송 객체
/// TCIA Collection data transfer object
/// </summary>
public sealed class TciaCollectionDto
{
    public string Collection { get; }
    public string? Description { get; }

    public TciaCollectionDto(string collection, string? description = null)
    {
        Collection = collection;
        Description = description;
    }
}

/// <summary>
/// TCIA Patient 전송 객체
/// TCIA Patient data transfer object
/// </summary>
public sealed class TciaPatientDto
{
    public string PatientId { get; }
    public string Collection { get; }
    public string? PatientName { get; }

    public TciaPatientDto(string patientId, string collection, string? patientName = null)
    {
        PatientId = patientId;
        Collection = collection;
        PatientName = patientName;
    }
}

/// <summary>
/// TCIA Study 전송 객체
/// TCIA Study data transfer object
/// </summary>
public sealed class TciaStudyDto
{
    public string StudyInstanceUID { get; }
    public string PatientId { get; }
    public string? PatientName { get; }
    public string? StudyDate { get; }
    public string? StudyDescription { get; }
    public int SeriesCount { get; }

    public TciaStudyDto(
        string studyInstanceUID,
        string patientId,
        string? patientName = null,
        string? studyDate = null,
        string? studyDescription = null,
        int seriesCount = 0)
    {
        StudyInstanceUID = studyInstanceUID;
        PatientId = patientId;
        PatientName = patientName;
        StudyDate = studyDate;
        StudyDescription = studyDescription;
        SeriesCount = seriesCount;
    }
}

/// <summary>
/// TCIA Series 전송 객체
/// TCIA Series data transfer object
/// </summary>
public sealed class TciaSeriesDto
{
    public string SeriesInstanceUID { get; }
    public string? SeriesDescription { get; }
    public string? Modality { get; }
    public string? BodyPartExamined { get; }
    public string? Manufacturer { get; }
    public int ImageCount { get; }
    public long FileSize { get; }

    public TciaSeriesDto(
        string seriesInstanceUID,
        string? seriesDescription = null,
        string? modality = null,
        string? bodyPartExamined = null,
        string? manufacturer = null,
        int imageCount = 0,
        long fileSize = 0)
    {
        SeriesInstanceUID = seriesInstanceUID;
        SeriesDescription = seriesDescription;
        Modality = modality;
        BodyPartExamined = bodyPartExamined;
        Manufacturer = manufacturer;
        ImageCount = imageCount;
        FileSize = fileSize;
    }
}

/// <summary>
/// DICOM 인스턴스 정보
/// DICOM instance information
/// </summary>
public sealed class DicomInstanceDto
{
    public string SopInstanceUid { get; }
    public int InstanceNumber { get; }
    public string FilePath { get; }
    public int Rows { get; }
    public int Columns { get; }
    public double? SliceLocation { get; }

    public DicomInstanceDto(
        string sopInstanceUid,
        int instanceNumber,
        string filePath,
        int rows = 0,
        int columns = 0,
        double? sliceLocation = null)
    {
        SopInstanceUid = sopInstanceUid;
        InstanceNumber = instanceNumber;
        FilePath = filePath;
        Rows = rows;
        Columns = columns;
        SliceLocation = sliceLocation;
    }
}

/// <summary>
/// DICOM 시리즈 정보
/// DICOM series information
/// </summary>
public sealed class DicomSeriesDto
{
    public string SeriesInstanceUid { get; }
    public string? SeriesDescription { get; }
    public int SeriesNumber { get; }
    public string? Modality { get; }
    public int NumberOfInstances { get; }
    public IReadOnlyList<DicomInstanceDto> Instances { get; }

    public DicomSeriesDto(
        string seriesInstanceUid,
        string? seriesDescription,
        int seriesNumber,
        string? modality,
        int numberOfInstances,
        IReadOnlyList<DicomInstanceDto>? instances = null)
    {
        SeriesInstanceUid = seriesInstanceUid;
        SeriesDescription = seriesDescription;
        SeriesNumber = seriesNumber;
        Modality = modality;
        NumberOfInstances = numberOfInstances;
        Instances = instances ?? Array.Empty<DicomInstanceDto>();
    }
}

/// <summary>
/// 다운로드 진행 정보
/// Download progress information
/// </summary>
public sealed class DownloadProgressInfo
{
    public long BytesDownloaded { get; }
    public long TotalBytes { get; }
    public double SpeedBytesPerSecond { get; }
    public TimeSpan ElapsedTime { get; }

    public double Progress => TotalBytes > 0 ? (double)BytesDownloaded / TotalBytes : 0;
    public double ProgressPercent => Progress * 100;

    public DownloadProgressInfo(
        long bytesDownloaded,
        long totalBytes,
        double speedBytesPerSecond,
        TimeSpan elapsedTime)
    {
        BytesDownloaded = bytesDownloaded;
        TotalBytes = totalBytes;
        SpeedBytesPerSecond = speedBytesPerSecond;
        ElapsedTime = elapsedTime;
    }
}
