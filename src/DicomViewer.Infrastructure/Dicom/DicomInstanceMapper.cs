namespace DicomViewer.Infrastructure.Dicom;

/// <summary>
/// DICOM 데이터셋 헤더를 Instance DTO로 변환 (픽셀 데이터 미사용)
/// Maps DICOM dataset headers to an instance DTO (does not use pixel data)
/// </summary>
internal static class DicomInstanceMapper
{
    public static DicomInstanceDto Map(DicomDataset dataset, string filePath) => new(
        SopInstanceUid: dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, string.Empty),
        InstanceNumber: dataset.GetSingleValueOrDefault(DicomTag.InstanceNumber, 0),
        FilePath: filePath,
        Rows: dataset.GetSingleValueOrDefault(DicomTag.Rows, (ushort)0),
        Columns: dataset.GetSingleValueOrDefault(DicomTag.Columns, (ushort)0),
        SeriesInstanceUid: ReadOptionalString(dataset, DicomTag.SeriesInstanceUID),
        FrameOfReferenceUid: ReadOptionalString(dataset, DicomTag.FrameOfReferenceUID),
        ImagePositionPatient: ReadOptionalDoubles(dataset, DicomTag.ImagePositionPatient),
        ImageOrientationPatient: ReadOptionalDoubles(dataset, DicomTag.ImageOrientationPatient));

    /// <summary>
    /// 태그가 없으면 null, 파싱할 수 없으면 빈 배열 (빈 배열은 이후 기하 판정에서 잘못된 값으로 처리)
    /// Null when the tag is absent; an empty array when it cannot be parsed (treated as malformed geometry later)
    /// </summary>
    private static double[]? ReadOptionalDoubles(DicomDataset dataset, DicomTag tag)
    {
        try
        {
            return dataset.TryGetValues<double>(tag, out var values) ? values : null;
        }
        catch (DicomException)
        {
            return [];
        }
    }

    private static string? ReadOptionalString(DicomDataset dataset, DicomTag tag)
    {
        try
        {
            return dataset.TryGetSingleValue<string>(tag, out var value) ? value : null;
        }
        catch (DicomException)
        {
            return null;
        }
    }
}