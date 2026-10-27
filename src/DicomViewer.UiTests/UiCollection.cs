[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace DicomViewer.UiTests;

/// <summary>
/// 마우스·포커스를 공유하므로 UI 테스트는 한 번에 하나만 실행한다.
/// UI tests share the mouse and focus, so they run strictly one at a time.
/// </summary>
[Xunit.CollectionDefinition(Name)]
public static class UiCollection
{
    public const string Name = "DicomViewer UI (non-parallel)";
}
