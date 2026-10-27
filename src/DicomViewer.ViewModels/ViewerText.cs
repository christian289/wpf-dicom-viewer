using System.Globalization;

namespace DicomViewer.ViewModels;

/// <summary>
/// 뷰어 상태·배너 문구 (한국어 / English)
/// Viewer status and banner texts (Korean / English)
/// </summary>
internal static class ViewerText
{
    public const string Empty = "데이터셋이 열려 있지 않습니다. / No dataset is open.";

    public const string Loading = "불러오는 중… / Loading…";

    public const string NoImages = "데이터셋에 표시할 영상이 없습니다. / The dataset contains no displayable images.";

    public const string GeometryMissing =
        "위치·방향 정보가 없어 슬라이스 순서가 실제 물리 순서가 아닙니다. / " +
        "Image position or orientation is missing, so the slice order is not the physical order.";

    public const string GeometryAmbiguous =
        "슬라이스 기하가 모호합니다(혼합·중복·불일치 프레임). 순서는 실제 물리 순서가 아닙니다. / " +
        "Slice geometry is ambiguous (mixed, duplicate or inconsistent frames); the order is not the physical order.";

    public const string SpacingUnknown =
        "픽셀 간격을 확인할 수 없어 측정은 px 단위입니다. / Pixel spacing is unknown, so measurements are in px.";

    public static string OpenFailed(string reason) =>
        $"데이터셋을 열 수 없습니다: {reason} / Could not open the dataset: {reason}";

    public static string LoadFailed(int instanceNumber, string reason) =>
        string.Create(CultureInfo.InvariantCulture,
            $"영상 {instanceNumber}을(를) 불러올 수 없습니다: {reason} / Could not load image {instanceNumber}: {reason}");

    public static string RenderFailed(string reason) =>
        $"영상을 표시할 수 없습니다: {reason} / Could not render the image: {reason}";

    public static string Ready(string datasetId, int sliceNumber, int sliceCount) =>
        string.Create(CultureInfo.InvariantCulture, $"{datasetId} · {sliceNumber} / {sliceCount}");

    public static string Distance(double value, PixelSpacingUnit unit) =>
        unit == PixelSpacingUnit.Millimeter
            ? string.Create(CultureInfo.InvariantCulture, $"{value:F2} mm")
            : string.Create(CultureInfo.InvariantCulture, $"{value:F1} px");
}