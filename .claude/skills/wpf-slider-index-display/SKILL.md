---
name: wpf-slider-index-display
description: 'WPF Slider에서 0-based 인덱스를 1-based로 사용자에게 표시하는 패턴'
---

# WPF Slider 0-based Index 표시 패턴

## 문제 상황

컬렉션의 인덱스를 슬라이더로 표시할 때, **내부적으로는 0-based 인덱스**를 사용하지만 **사용자에게는 1-based**로 표시해야 하는 경우가 많습니다.

### 증상
- 120개 이미지가 있는데 "0 / 120" ~ "119 / 120"으로 표시됨
- 사용자는 "1 / 120" ~ "120 / 120"을 기대함
- 슬라이더를 끝까지 드래그해도 마지막 항목에 도달하지 못함

### 원인
- 슬라이더의 `Maximum`이 `TotalCount`로 설정되어 있으면 범위 초과
- 0-based 인덱스를 그대로 화면에 표시하면 사용자에게 혼란

---

## 해결 방법

### ViewModel에 표시용 속성 추가

```csharp
public partial class ViewerViewModel : ObservableObject
{
    // 내부 인덱스 (0-based)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SliceDisplayNumber))]
    private int _currentSliceIndex;

    // 전체 개수
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaxSliceIndex))]
    private int _totalSliceCount;

    /// <summary>
    /// 슬라이더 Maximum 값 (0-based 인덱스 최대값)
    /// </summary>
    public int MaxSliceIndex => Math.Max(0, TotalSliceCount - 1);

    /// <summary>
    /// 사용자 표시용 번호 (1-based)
    /// </summary>
    public int SliceDisplayNumber => CurrentSliceIndex + 1;
}
```

### XAML 바인딩

```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
    </Grid.ColumnDefinitions>

    <!-- 현재 번호 (1-based 표시) -->
    <TextBlock Grid.Column="0"
               Text="{Binding SliceDisplayNumber}" />

    <!-- 슬라이더 (0-based 인덱스, Maximum은 Count-1) -->
    <Slider Grid.Column="1"
            Minimum="0"
            Maximum="{Binding MaxSliceIndex}"
            Value="{Binding CurrentSliceIndex}" />

    <!-- 전체 개수 -->
    <TextBlock Grid.Column="2"
               Text="{Binding TotalSliceCount}" />
</Grid>
```

---

## 핵심 포인트

| 속성 | 값 범위 | 용도 |
|------|---------|------|
| `CurrentSliceIndex` | 0 ~ (Count-1) | 내부 로직, 슬라이더 Value |
| `MaxSliceIndex` | Count-1 | 슬라이더 Maximum |
| `SliceDisplayNumber` | 1 ~ Count | 사용자 표시 |
| `TotalSliceCount` | Count | 전체 개수 표시 |

---

## NotifyPropertyChangedFor 사용

`[NotifyPropertyChangedFor]` 어트리뷰트를 사용하면 소스 속성이 변경될 때 계산된 속성의 `PropertyChanged`도 자동으로 발생합니다.

```csharp
// CurrentSliceIndex가 변경되면 SliceDisplayNumber도 PropertyChanged 발생
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(SliceDisplayNumber))]
private int _currentSliceIndex;

// TotalSliceCount가 변경되면 MaxSliceIndex도 PropertyChanged 발생
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(MaxSliceIndex))]
private int _totalSliceCount;
```

---

## 적용 예시

- 이미지 뷰어의 슬라이스 네비게이션 (CT/MRI)
- 페이지네이션 (문서 뷰어)
- 미디어 플레이어의 트랙 목록
- 갤러리의 이미지 인덱스
