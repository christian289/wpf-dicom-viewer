namespace DicomViewer.Application.DTOs;

/// <summary>
/// 검증을 마친 DICOM 데이터셋 전송 객체. 시리즈·인스턴스 값은 파일 헤더에서 읽은 값이다.
/// Verified DICOM dataset transfer object. Series and instance values were read from file headers.
/// </summary>
public sealed record DicomDatasetDto(
    string DatasetId,
    string Attribution,
    string Classification,
    IReadOnlyList<DicomSeriesDto> Series);