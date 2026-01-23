using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DicomViewer.UI.Controls;

/// <summary>
/// CT 슬라이스 네비게이션 컨트롤
/// CT slice navigation control
/// </summary>
public sealed class SliceNavigator : Control
{
    // XAML 바인딩용 정적 컨버터
    // Static converters for XAML binding
    public static readonly IValueConverter IndexToDisplayConverter = new IndexToDisplayValueConverter();
    public static readonly IValueConverter CountToMaxConverter = new CountToMaxValueConverter();

    // Freeze된 렌더링 리소스 (성능 최적화)
    // Frozen rendering resources (performance optimization)
    private static readonly Brush SelectedBorderBrush;
    private static readonly Brush NormalBorderBrush;
    private static readonly Brush BackgroundBrush;

    static SliceNavigator()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SliceNavigator),
            new FrameworkPropertyMetadata(typeof(SliceNavigator)));

        SelectedBorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
        SelectedBorderBrush.Freeze();

        NormalBorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));
        NormalBorderBrush.Freeze();

        BackgroundBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        BackgroundBrush.Freeze();
    }

    #region Dependency Properties

    // 썸네일 컬렉션
    // Thumbnails collection
    public static readonly DependencyProperty ThumbnailsProperty =
        DependencyProperty.Register(
            nameof(Thumbnails),
            typeof(ObservableCollection<ThumbnailItem>),
            typeof(SliceNavigator),
            new PropertyMetadata(null));

    public ObservableCollection<ThumbnailItem>? Thumbnails
    {
        get => (ObservableCollection<ThumbnailItem>?)GetValue(ThumbnailsProperty);
        set => SetValue(ThumbnailsProperty, value);
    }

    // 현재 선택된 인덱스
    // Currently selected index
    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(
            nameof(SelectedIndex),
            typeof(int),
            typeof(SliceNavigator),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedIndexChanged));

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    // 총 슬라이스 수
    // Total slice count
    public static readonly DependencyProperty TotalCountProperty =
        DependencyProperty.Register(
            nameof(TotalCount),
            typeof(int),
            typeof(SliceNavigator),
            new PropertyMetadata(0));

    public int TotalCount
    {
        get => (int)GetValue(TotalCountProperty);
        set => SetValue(TotalCountProperty, value);
    }

    // 썸네일 크기
    // Thumbnail size
    public static readonly DependencyProperty ThumbnailSizeProperty =
        DependencyProperty.Register(
            nameof(ThumbnailSize),
            typeof(double),
            typeof(SliceNavigator),
            new PropertyMetadata(64.0));

    public double ThumbnailSize
    {
        get => (double)GetValue(ThumbnailSizeProperty);
        set => SetValue(ThumbnailSizeProperty, value);
    }

    // 방향 (Horizontal/Vertical)
    // Orientation (Horizontal/Vertical)
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(SliceNavigator),
            new PropertyMetadata(Orientation.Vertical));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    // 슬라이더 표시 여부
    // Show slider
    public static readonly DependencyProperty ShowSliderProperty =
        DependencyProperty.Register(
            nameof(ShowSlider),
            typeof(bool),
            typeof(SliceNavigator),
            new PropertyMetadata(true));

    public bool ShowSlider
    {
        get => (bool)GetValue(ShowSliderProperty);
        set => SetValue(ShowSliderProperty, value);
    }

    // 썸네일 표시 여부
    // Show thumbnails
    public static readonly DependencyProperty ShowThumbnailsProperty =
        DependencyProperty.Register(
            nameof(ShowThumbnails),
            typeof(bool),
            typeof(SliceNavigator),
            new PropertyMetadata(true));

    public bool ShowThumbnails
    {
        get => (bool)GetValue(ShowThumbnailsProperty);
        set => SetValue(ShowThumbnailsProperty, value);
    }

    #endregion

    #region Routed Events

    // 선택 변경 이벤트
    // Selection changed event
    public static readonly RoutedEvent SelectionChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(SelectionChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<int>),
            typeof(SliceNavigator));

    public event RoutedPropertyChangedEventHandler<int> SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    #endregion

    // 캐시된 템플릿 자식 요소 (이벤트 핸들러 관리용)
    // Cached template child elements (for event handler management)
    private ListBox? _thumbnailList;
    private Slider? _slider;

    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SliceNavigator navigator)
        {
            var oldValue = (int)e.OldValue;
            var newValue = (int)e.NewValue;

            navigator.RaiseEvent(new RoutedPropertyChangedEventArgs<int>(
                oldValue, newValue, SelectionChangedEvent));
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 기존 핸들러 해제 (메모리 누수 방지)
        // Unsubscribe existing handlers (prevent memory leak)
        if (_thumbnailList is not null)
        {
            _thumbnailList.SelectionChanged -= OnThumbnailListSelectionChanged;
        }
        if (_slider is not null)
        {
            _slider.ValueChanged -= OnSliderValueChanged;
        }

        // 새 요소 캐싱 및 핸들러 등록
        // Cache new elements and register handlers
        _thumbnailList = GetTemplateChild("PART_ThumbnailList") as ListBox;
        _slider = GetTemplateChild("PART_Slider") as Slider;

        if (_thumbnailList is not null)
        {
            _thumbnailList.SelectionChanged += OnThumbnailListSelectionChanged;
        }
        if (_slider is not null)
        {
            _slider.ValueChanged += OnSliderValueChanged;
        }
    }

    private void OnThumbnailListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedIndex >= 0)
        {
            SelectedIndex = listBox.SelectedIndex;
        }
    }

    private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        SelectedIndex = (int)e.NewValue;
    }
}

/// <summary>
/// 썸네일 아이템 (바인딩용)
/// Thumbnail item (for binding)
/// </summary>
public sealed class ThumbnailItem : INotifyPropertyChanged
{
    private ImageSource? _imageSource;
    private bool _isSelected;

    public int Index { get; init; }

    public int DisplayNumber => Index + 1;

    public ImageSource? ImageSource
    {
        get => _imageSource;
        set
        {
            _imageSource = value;
            OnPropertyChanged(nameof(ImageSource));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public string? Label { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 0-based 인덱스를 1-based 표시 번호로 변환
/// Convert 0-based index to 1-based display number
/// </summary>
internal sealed class IndexToDisplayValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index)
            return index + 1;
        return 1;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int display)
            return display - 1;
        return 0;
    }
}

/// <summary>
/// 총 개수를 슬라이더 Maximum 값으로 변환 (count - 1)
/// Convert total count to slider maximum value (count - 1)
/// </summary>
internal sealed class CountToMaxValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
            return Math.Max(0, count - 1);
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int max)
            return max + 1;
        return 1;
    }
}
