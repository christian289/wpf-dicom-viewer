using DicomViewer.ViewModels.Messages;

namespace DicomViewer.ViewModels;

/// <summary>
/// Study 목록 ViewModel
/// Study list ViewModel
/// </summary>
public sealed partial class StudyListViewModel : ObservableRecipient
{
    private readonly DicomStudyService _studyService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<DicomStudyDto> _studies = [];

    [ObservableProperty]
    private DicomStudyDto? _selectedStudy;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _patientIdFilter = string.Empty;

    [ObservableProperty]
    private string _patientNameFilter = string.Empty;

    [ObservableProperty]
    private DateTime? _studyDateFrom;

    [ObservableProperty]
    private DateTime? _studyDateTo;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    public StudyListViewModel(
        DicomStudyService studyService,
        IDialogService dialogService)
    {
        _studyService = studyService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        var folderPath = _dialogService.ShowOpenFolderDialog("DICOM 폴더 선택 / Select DICOM Folder");
        if (string.IsNullOrEmpty(folderPath)) return;

        CurrentPath = folderPath;
        await SearchStudiesAsync();
    }

    [RelayCommand]
    private async Task SearchStudiesAsync()
    {
        if (string.IsNullOrEmpty(CurrentPath))
        {
            Messenger.Send(new StatusMessage("폴더를 먼저 선택하세요. / Please select a folder first.", true));
            return;
        }

        IsLoading = true;
        Messenger.Send(new StatusMessage("Study 검색 중... / Searching studies..."));

        try
        {
            var results = await _studyService.SearchStudiesAsync(
                PatientIdFilter,
                PatientNameFilter,
                StudyDateFrom,
                StudyDateTo);

            Studies = new ObservableCollection<DicomStudyDto>(results);
            Messenger.Send(new StatusMessage($"{Studies.Count}개의 Study를 찾았습니다. / Found {Studies.Count} studies."));
        }
        catch (Exception ex)
        {
            Messenger.Send(new StatusMessage($"검색 실패: {ex.Message} / Search failed: {ex.Message}", true));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        PatientIdFilter = string.Empty;
        PatientNameFilter = string.Empty;
        StudyDateFrom = null;
        StudyDateTo = null;
    }

    [RelayCommand]
    private void OpenStudy()
    {
        if (SelectedStudy is null) return;

        Messenger.Send(new StudySelectedMessage(SelectedStudy));
    }

    partial void OnSelectedStudyChanged(DicomStudyDto? value)
    {
        OpenStudyCommand.NotifyCanExecuteChanged();
    }
}
