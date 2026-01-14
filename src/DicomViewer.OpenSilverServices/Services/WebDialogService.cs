using DicomViewer.OpenSilverServices.Interfaces;
using OpenSilver;

namespace DicomViewer.OpenSilverServices.Services;

/// <summary>
/// 웹 환경용 다이얼로그 서비스
/// Dialog service for web environment
/// </summary>
public sealed class WebDialogService : IDialogService
{
    public void ShowMessage(string title, string message)
    {
        // JavaScript alert 사용
        // Use JavaScript alert
        var escapedMessage = EscapeJavaScript($"{title}\n\n{message}");
        Interop.ExecuteJavaScript($"alert('{escapedMessage}')");
    }

    public void ShowError(string title, string message)
    {
        // 에러 표시 (alert 사용)
        // Show error (using alert)
        var escapedMessage = EscapeJavaScript($"Error: {title}\n\n{message}");
        Interop.ExecuteJavaScript($"alert('{escapedMessage}')");
    }

    public bool ShowConfirmation(string title, string message)
    {
        // JavaScript confirm 사용
        // Use JavaScript confirm
        var escapedMessage = EscapeJavaScript($"{title}\n\n{message}");
        var result = Interop.ExecuteJavaScript($"confirm('{escapedMessage}')");
        return result?.ToString()?.ToLower() == "true";
    }

    private static string EscapeJavaScript(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }
}
