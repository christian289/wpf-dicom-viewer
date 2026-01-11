namespace DicomViewer.WpfServices;

/// <summary>
/// WPF 다이얼로그 서비스 구현
/// WPF dialog service implementation
/// </summary>
public sealed class DialogService : IDialogService
{
    public string? ShowOpenFolderDialog(string? title = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title ?? "폴더 선택 / Select Folder"
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? ShowOpenFileDialog(string filter, string? title = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title ?? "파일 선택 / Select File",
            Filter = filter
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public void ShowMessage(string title, string message)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    public bool ShowConfirmation(string title, string message)
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }
}
