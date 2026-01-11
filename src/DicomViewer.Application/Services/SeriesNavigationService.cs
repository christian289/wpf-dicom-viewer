using DicomViewer.Application.DTOs;
using DicomViewer.Application.Interfaces;

namespace DicomViewer.Application.Services;

/// <summary>
/// 시리즈 네비게이션 서비스 구현
/// Series navigation service implementation
/// </summary>
public sealed class SeriesNavigationService : ISeriesNavigationService
{
    private IReadOnlyList<DicomInstanceDto> _instances = [];
    private int _currentIndex;

    public int CurrentIndex => _currentIndex;
    public int TotalCount => _instances.Count;

    public void SetSeries(IReadOnlyList<DicomInstanceDto> instances)
    {
        _instances = instances
            .OrderBy(i => i.SliceLocation ?? i.InstanceNumber)
            .ToList();
        _currentIndex = 0;
    }

    public DicomInstanceDto? GetCurrentInstance()
    {
        if (_instances.Count == 0) return null;
        return _instances[_currentIndex];
    }

    public DicomInstanceDto? NavigateToIndex(int index)
    {
        if (_instances.Count == 0) return null;

        _currentIndex = Math.Clamp(index, 0, _instances.Count - 1);
        return _instances[_currentIndex];
    }

    public DicomInstanceDto? NavigateNext()
    {
        if (_instances.Count == 0) return null;
        if (_currentIndex >= _instances.Count - 1) return _instances[_currentIndex];

        _currentIndex++;
        return _instances[_currentIndex];
    }

    public DicomInstanceDto? NavigatePrevious()
    {
        if (_instances.Count == 0) return null;
        if (_currentIndex <= 0) return _instances[_currentIndex];

        _currentIndex--;
        return _instances[_currentIndex];
    }

    public DicomInstanceDto? NavigateFirst()
    {
        if (_instances.Count == 0) return null;

        _currentIndex = 0;
        return _instances[_currentIndex];
    }

    public DicomInstanceDto? NavigateLast()
    {
        if (_instances.Count == 0) return null;

        _currentIndex = _instances.Count - 1;
        return _instances[_currentIndex];
    }

    public void Clear()
    {
        _instances = [];
        _currentIndex = 0;
    }
}
