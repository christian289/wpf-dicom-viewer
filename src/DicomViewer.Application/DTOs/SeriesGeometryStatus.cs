namespace DicomViewer.Application.DTOs;

/// <summary>
/// 시리즈 공간 정렬 상태
/// Series spatial ordering status
/// </summary>
public enum SeriesGeometryStatus
{
    /// <summary>시리즈에 인스턴스가 없음 / No instances</summary>
    Empty,

    /// <summary>모든 인스턴스의 위치·방향이 일관되어 물리 순서 사용 가능 / Consistent geometry; physical order is used</summary>
    Spatial,

    /// <summary>일부 또는 전체 인스턴스에 ImagePositionPatient/ImageOrientationPatient 없음 / Positions or orientations are absent; order is not physical</summary>
    MissingGeometry,

    /// <summary>값이 잘못되었거나 혼합·중복·불일치 프레임 / Malformed, mixed, duplicate, or inconsistent frames; order is not physical</summary>
    AmbiguousGeometry,
}