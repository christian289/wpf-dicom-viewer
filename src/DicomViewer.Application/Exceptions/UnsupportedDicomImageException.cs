namespace DicomViewer.Application.Exceptions;

/// <summary>
/// 현재 뷰어가 지원하지 않는 DICOM 영상 형식
/// DICOM image format the viewer does not support
/// </summary>
public sealed class UnsupportedDicomImageException : Exception
{
    public UnsupportedDicomImageException(string message)
        : base(message)
    {
    }
}