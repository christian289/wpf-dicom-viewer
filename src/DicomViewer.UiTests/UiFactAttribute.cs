namespace DicomViewer.UiTests;

/// <summary>
/// UI 검증 전용 Fact. 명시적 옵트인(DICOM_VIEWER_RUN_UI_TESTS=1)이 없으면 통과가 아니라 Skip으로 보고한다.
/// UI-verification-only fact. Without explicit opt-in (DICOM_VIEWER_RUN_UI_TESTS=1) it reports Skip, never a silent pass.
/// </summary>
public sealed class UiFactAttribute : FactAttribute
{
    public const string EnableVariable = "DICOM_VIEWER_RUN_UI_TESTS";

    public UiFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnableVariable), "1", StringComparison.Ordinal))
        {
            Skip = $"UI verification is opt-in. Set {EnableVariable}=1 and DICOM_VIEWER_APP_EXE to run it.";
        }
    }
}
