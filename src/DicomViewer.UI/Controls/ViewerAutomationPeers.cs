using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace DicomViewer.UI.Controls;

/// <summary>
/// 표시 중인 DICOM 영상 영역을 UI 자동화에 이미지로 노출한다. 이름과 AutomationId는 XAML의 AutomationProperties가 제공한다.
/// Exposes the displayed DICOM image area to UI Automation as an image. Name and AutomationId come from AutomationProperties in XAML.
/// </summary>
internal sealed class DicomImageViewerAutomationPeer(DicomImageViewer owner) : FrameworkElementAutomationPeer(owner)
{
    protected override string GetClassNameCore() => nameof(DicomImageViewer);

    // 영상 영역은 하나의 이미지로 노출한다. 템플릿 내부(이미지 조각, 오버레이 칩)는 중복이므로 자식으로 노출하지 않는다.
    // The image area is exposed as one image; template internals (image part, overlay chips) are duplicates and are not exposed as children.
    protected override List<AutomationPeer>? GetChildrenCore() => null;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
}

/// <summary>
/// 윈도우 적용된 영상의 히스토그램을 이미지로 노출한다 (그리기 전용 요소라 기본 피어가 없다).
/// Exposes the histogram of the windowed image as an image (a draw-only element has no default peer).
/// </summary>
internal sealed class HistogramViewerAutomationPeer(HistogramViewer owner) : FrameworkElementAutomationPeer(owner)
{
    protected override string GetClassNameCore() => nameof(HistogramViewer);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
}

/// <summary>
/// 로딩 표시기를 진행 표시줄로 노출한다. 이름이 없으면 표시 문구, 그것도 없으면 "Loading"이다. 비활성이면 컨트롤이 접히므로 화면 밖으로 보고된다.
/// Exposes the loading indicator as a progress bar. The name falls back to the displayed text, then to "Loading". When inactive the control
/// is collapsed and therefore reported off-screen.
/// </summary>
internal sealed class LoadingIndicatorAutomationPeer(LoadingIndicator owner) : FrameworkElementAutomationPeer(owner)
{
    protected override string GetClassNameCore() => nameof(LoadingIndicator);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        if (!string.IsNullOrEmpty(name))
        {
            return name;
        }

        var text = ((LoadingIndicator)Owner).Text;
        return string.IsNullOrEmpty(text) ? "Loading" : text;
    }
}