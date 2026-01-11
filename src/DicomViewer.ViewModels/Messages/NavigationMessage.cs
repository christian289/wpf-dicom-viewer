namespace DicomViewer.ViewModels.Messages;

/// <summary>
/// 네비게이션 메시지
/// Navigation message
/// </summary>
public sealed record NavigationMessage(string ViewName, object? Parameter = null);

/// <summary>
/// Study 선택 메시지
/// Study selection message
/// </summary>
public sealed record StudySelectedMessage(DicomStudyDto Study);

/// <summary>
/// Series 선택 메시지
/// Series selection message
/// </summary>
public sealed record SeriesSelectedMessage(DicomSeriesDto Series);

/// <summary>
/// 상태 메시지
/// Status message
/// </summary>
public sealed record StatusMessage(string Message, bool IsError = false);

/// <summary>
/// 폴더 열기 메시지 (TCIA 다운로드 후 뷰어로 전달)
/// Open folder message (pass to viewer after TCIA download)
/// </summary>
public sealed record OpenFolderMessage(string FolderPath);
