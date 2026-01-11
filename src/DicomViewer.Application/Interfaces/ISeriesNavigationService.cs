using DicomViewer.Application.DTOs;

namespace DicomViewer.Application.Interfaces;

/// <summary>
/// 시리즈 네비게이션 서비스 인터페이스
/// Series navigation service interface
/// </summary>
public interface ISeriesNavigationService
{
    /// <summary>
    /// 현재 인덱스
    /// Current index
    /// </summary>
    int CurrentIndex { get; }

    /// <summary>
    /// 전체 개수
    /// Total count
    /// </summary>
    int TotalCount { get; }

    /// <summary>
    /// 시리즈 설정
    /// Set series
    /// </summary>
    void SetSeries(IReadOnlyList<DicomInstanceDto> instances);

    /// <summary>
    /// 현재 인스턴스 가져오기
    /// Get current instance
    /// </summary>
    DicomInstanceDto? GetCurrentInstance();

    /// <summary>
    /// 특정 인덱스로 이동
    /// Navigate to specific index
    /// </summary>
    DicomInstanceDto? NavigateToIndex(int index);

    /// <summary>
    /// 다음으로 이동
    /// Navigate to next
    /// </summary>
    DicomInstanceDto? NavigateNext();

    /// <summary>
    /// 이전으로 이동
    /// Navigate to previous
    /// </summary>
    DicomInstanceDto? NavigatePrevious();

    /// <summary>
    /// 처음으로 이동
    /// Navigate to first
    /// </summary>
    DicomInstanceDto? NavigateFirst();

    /// <summary>
    /// 마지막으로 이동
    /// Navigate to last
    /// </summary>
    DicomInstanceDto? NavigateLast();

    /// <summary>
    /// 시리즈 초기화
    /// Clear series
    /// </summary>
    void Clear();
}
