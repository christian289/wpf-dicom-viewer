namespace DicomViewer.OpenSilverServices.Interfaces;

/// <summary>
/// 다이얼로그 서비스 인터페이스
/// Dialog service interface
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// 정보 메시지 표시
    /// Show information message
    /// </summary>
    void ShowMessage(string title, string message);

    /// <summary>
    /// 에러 메시지 표시
    /// Show error message
    /// </summary>
    void ShowError(string title, string message);

    /// <summary>
    /// 확인 다이얼로그 표시
    /// Show confirmation dialog
    /// </summary>
    bool ShowConfirmation(string title, string message);
}
