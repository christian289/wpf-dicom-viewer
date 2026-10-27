namespace DicomViewer.ViewModels;

/// <summary>
/// 뷰어의 표시 상태
/// Viewer display state
/// </summary>
public enum ViewerState
{
    /// <summary>표시할 영상이 없음 / Nothing to display</summary>
    Empty,

    /// <summary>데이터셋 열기 또는 슬라이스 로드가 진행 중 / Dataset open or slice load in flight</summary>
    Loading,

    /// <summary>영상이 표시되고 있음 / An image is displayed</summary>
    Ready,

    /// <summary>열기·로드·렌더 오류로 표시할 수 없음 / Open, load or render failed</summary>
    Error
}