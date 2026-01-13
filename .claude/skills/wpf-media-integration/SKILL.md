---
name: wpf-media-integration
description: WPF 미디어 통합. MediaElement 비디오/오디오 재생, Image 컨트롤 이미지 표시, SoundPlayerAction 사운드 효과. 미디어 플레이어, 갤러리, 멀티미디어 UI 구현 시 이 스킬 적용.
---

# WPF Media Integration 패턴

WPF에서 이미지, 비디오, 오디오 등 멀티미디어 콘텐츠를 통합합니다.

## 1. Image 컨트롤

### 1.1 기본 이미지 표시

```xml
<!-- 리소스 이미지 -->
<!-- Resource image -->
<Image Source="/Assets/logo.png" Width="100" Height="100"/>

<!-- 절대 경로 -->
<!-- Absolute path -->
<Image Source="C:\Images\photo.jpg"/>

<!-- URI -->
<Image Source="https://example.com/image.png"/>

<!-- Pack URI (임베디드 리소스) -->
<!-- Pack URI (embedded resource) -->
<Image Source="pack://application:,,,/MyAssembly;component/Images/icon.png"/>
```

### 1.2 Stretch 옵션

```xml
<!-- None: 원본 크기 유지 -->
<!-- None: maintain original size -->
<Image Source="/photo.jpg" Stretch="None"/>

<!-- Fill: 영역에 맞게 늘림 (비율 무시) -->
<!-- Fill: stretch to fit area (ignore aspect ratio) -->
<Image Source="/photo.jpg" Stretch="Fill"/>

<!-- Uniform: 비율 유지, 영역 내 최대 크기 -->
<!-- Uniform: maintain aspect ratio, maximum size within area -->
<Image Source="/photo.jpg" Stretch="Uniform"/>

<!-- UniformToFill: 비율 유지, 영역 채움 (잘릴 수 있음) -->
<!-- UniformToFill: maintain aspect ratio, fill area (may crop) -->
<Image Source="/photo.jpg" Stretch="UniformToFill"/>
```

### 1.3 동적 이미지 로드

```csharp
namespace MyApp.Helpers;

using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class ImageHelper
{
    /// <summary>
    /// 파일에서 이미지 로드
    /// Load image from file
    /// </summary>
    public static BitmapImage LoadFromFile(string filePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze(); // UI 스레드 외부에서 사용 가능
                         // Can be used outside UI thread
        return bitmap;
    }

    /// <summary>
    /// 스트림에서 이미지 로드
    /// Load image from stream
    /// </summary>
    public static BitmapImage LoadFromStream(Stream stream)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = stream;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// 썸네일 로드 (메모리 최적화)
    /// Load thumbnail (memory optimization)
    /// </summary>
    public static BitmapImage LoadThumbnail(string filePath, int maxWidth, int maxHeight)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
        bitmap.DecodePixelWidth = maxWidth;
        bitmap.DecodePixelHeight = maxHeight;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Base64에서 이미지 로드
    /// Load image from Base64
    /// </summary>
    public static BitmapImage LoadFromBase64(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        using var stream = new MemoryStream(bytes);
        return LoadFromStream(stream);
    }
}
```

### 1.4 이미지 로드 이벤트

```csharp
// XAML
// <Image x:Name="DynamicImage" ImageFailed="OnImageFailed"/>

private void LoadImageAsync(string url)
{
    var bitmap = new BitmapImage();
    bitmap.BeginInit();
    bitmap.UriSource = new Uri(url);
    bitmap.EndInit();
    
    // 로딩 완료 이벤트
    // Loading complete event
    bitmap.DownloadCompleted += (s, e) =>
    {
        // 이미지 로드 완료
        // Image load complete
    };
    
    // 로딩 실패 이벤트
    // Loading failed event
    bitmap.DownloadFailed += (s, e) =>
    {
        // 오류 처리
        // Error handling
    };
    
    // 로딩 진행률
    // Loading progress
    bitmap.DownloadProgress += (s, e) =>
    {
        var progress = e.Progress; // 0-100
    };
    
    DynamicImage.Source = bitmap;
}
```

---

## 2. MediaElement (비디오/오디오)

### 2.1 기본 사용

```xml
<!-- 자동 재생 비디오 -->
<!-- Auto-play video -->
<MediaElement Source="/Videos/intro.mp4" 
              LoadedBehavior="Play" 
              UnloadedBehavior="Stop"/>

<!-- 수동 제어 비디오 -->
<!-- Manual control video -->
<MediaElement x:Name="VideoPlayer" 
              Source="/Videos/movie.mp4"
              LoadedBehavior="Manual"
              UnloadedBehavior="Stop"
              MediaOpened="OnMediaOpened"
              MediaEnded="OnMediaEnded"
              MediaFailed="OnMediaFailed"/>
```

### 2.2 LoadedBehavior 옵션

| 값 | 설명 |
|-----|------|
| **Play** | 자동 재생 |
| **Pause** | 로드 후 일시정지 |
| **Stop** | 로드 후 정지 |
| **Manual** | 코드로 제어 |
| **Close** | 미디어 닫기 |

### 2.3 비디오 플레이어 구현

```csharp
namespace MyApp.Controls;

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

public sealed partial class VideoPlayerControl : UserControl
{
    private readonly DispatcherTimer _positionTimer;
    private bool _isDragging;

    public VideoPlayerControl()
    {
        InitializeComponent();
        
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _positionTimer.Tick += OnPositionTimerTick;
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        // 총 재생 시간 표시
        // Display total duration
        if (VideoPlayer.NaturalDuration.HasTimeSpan)
        {
            var duration = VideoPlayer.NaturalDuration.TimeSpan;
            PositionSlider.Maximum = duration.TotalSeconds;
            TotalTimeText.Text = FormatTime(duration);
        }
        
        _positionTimer.Start();
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        _positionTimer.Stop();
        VideoPlayer.Stop();
        VideoPlayer.Position = TimeSpan.Zero;
    }

    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        // 미디어 로드 실패
        // Media load failed
        MessageBox.Show($"미디어 로드 실패: {e.ErrorException.Message}");
        // MessageBox.Show($"Media load failed: {e.ErrorException.Message}");
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (!_isDragging)
        {
            PositionSlider.Value = VideoPlayer.Position.TotalSeconds;
            CurrentTimeText.Text = FormatTime(VideoPlayer.Position);
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        VideoPlayer.Play();
        _positionTimer.Start();
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        VideoPlayer.Pause();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        VideoPlayer.Stop();
        _positionTimer.Stop();
    }

    private void PositionSlider_DragStarted(object sender, EventArgs e)
    {
        _isDragging = true;
    }

    private void PositionSlider_DragCompleted(object sender, EventArgs e)
    {
        _isDragging = false;
        VideoPlayer.Position = TimeSpan.FromSeconds(PositionSlider.Value);
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        VideoPlayer.Volume = e.NewValue;
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.Hours > 0
            ? $"{time:hh\\:mm\\:ss}"
            : $"{time:mm\\:ss}";
    }
}
```

### 2.4 VideoPlayerControl XAML

```xml
<UserControl x:Class="MyApp.Controls.VideoPlayerControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
        <!-- 비디오 영역 -->
        <!-- Video area -->
        <MediaElement x:Name="VideoPlayer"
                      LoadedBehavior="Manual"
                      UnloadedBehavior="Stop"
                      MediaOpened="OnMediaOpened"
                      MediaEnded="OnMediaEnded"
                      MediaFailed="OnMediaFailed"/>
        
        <!-- 컨트롤 바 -->
        <!-- Control bar -->
        <Grid Grid.Row="1" Background="#CC000000">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            
            <Button x:Name="PlayButton" Content="▶" Click="PlayButton_Click" 
                    Grid.Column="0" Margin="5"/>
            <Button x:Name="PauseButton" Content="⏸" Click="PauseButton_Click" 
                    Grid.Column="1" Margin="5"/>
            <Button x:Name="StopButton" Content="⏹" Click="StopButton_Click" 
                    Grid.Column="2" Margin="5"/>
            
            <!-- 재생 위치 슬라이더 -->
            <!-- Position slider -->
            <Slider x:Name="PositionSlider" 
                    Grid.Column="3" 
                    Margin="5"
                    VerticalAlignment="Center"
                    Thumb.DragStarted="PositionSlider_DragStarted"
                    Thumb.DragCompleted="PositionSlider_DragCompleted"/>
            
            <!-- 시간 표시 -->
            <!-- Time display -->
            <StackPanel Grid.Column="4" Orientation="Horizontal" 
                        VerticalAlignment="Center" Margin="5">
                <TextBlock x:Name="CurrentTimeText" Foreground="White" Text="00:00"/>
                <TextBlock Foreground="White" Text=" / "/>
                <TextBlock x:Name="TotalTimeText" Foreground="White" Text="00:00"/>
            </StackPanel>
            
            <!-- 볼륨 슬라이더 -->
            <!-- Volume slider -->
            <Slider x:Name="VolumeSlider" 
                    Grid.Column="5" 
                    Width="80" 
                    Margin="5"
                    Minimum="0" Maximum="1" Value="0.5"
                    VerticalAlignment="Center"
                    ValueChanged="VolumeSlider_ValueChanged"/>
        </Grid>
    </Grid>
</UserControl>
```

---

## 3. 오디오 재생

### 3.1 MediaElement로 오디오

```xml
<MediaElement x:Name="AudioPlayer" 
              Source="/Sounds/background.mp3"
              LoadedBehavior="Manual"
              Volume="0.5"/>
```

### 3.2 SoundPlayer (단순 WAV)

```csharp
namespace MyApp.Services;

using System.Media;

public sealed class SoundService
{
    private readonly SoundPlayer _clickSound;
    private readonly SoundPlayer _notificationSound;

    public SoundService()
    {
        // WAV 파일만 지원
        // WAV files only
        _clickSound = new SoundPlayer("Sounds/click.wav");
        _notificationSound = new SoundPlayer("Sounds/notification.wav");
        
        // 미리 로드
        // Preload
        _clickSound.Load();
        _notificationSound.Load();
    }

    public void PlayClick()
    {
        _clickSound.Play(); // 비동기 재생
                            // Async playback
    }

    public void PlayNotification()
    {
        _notificationSound.PlaySync(); // 동기 재생
                                        // Sync playback
    }
}
```

### 3.3 SoundPlayerAction (XAML에서 사운드)

```xml
<Button Content="Click Me">
    <Button.Triggers>
        <EventTrigger RoutedEvent="Button.Click">
            <SoundPlayerAction Source="/Sounds/click.wav"/>
        </EventTrigger>
    </Button.Triggers>
</Button>
```

---

## 4. 이미지 갤러리 예제

### 4.1 갤러리 ViewModel

```csharp
namespace MyApp.ViewModels;

using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed partial class GalleryViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<BitmapImage> _images = [];

    [ObservableProperty]
    private BitmapImage? _selectedImage;

    [ObservableProperty]
    private int _selectedIndex;

    [RelayCommand]
    private void LoadFolder(string folderPath)
    {
        Images.Clear();
        
        var extensions = new[] { "*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp" };
        
        foreach (var ext in extensions)
        {
            foreach (var file in Directory.GetFiles(folderPath, ext))
            {
                var thumbnail = LoadThumbnail(file);
                Images.Add(thumbnail);
            }
        }
        
        if (Images.Count > 0)
        {
            SelectedImage = Images[0];
            SelectedIndex = 0;
        }
    }

    [RelayCommand]
    private void NextImage()
    {
        if (Images.Count is 0)
        {
            return;
        }
        
        SelectedIndex = (SelectedIndex + 1) % Images.Count;
        SelectedImage = Images[SelectedIndex];
    }

    [RelayCommand]
    private void PreviousImage()
    {
        if (Images.Count is 0)
        {
            return;
        }
        
        SelectedIndex = (SelectedIndex - 1 + Images.Count) % Images.Count;
        SelectedImage = Images[SelectedIndex];
    }

    private static BitmapImage LoadThumbnail(string filePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(filePath);
        bitmap.DecodePixelWidth = 200;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
```

### 4.2 갤러리 View

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="100"/>
    </Grid.RowDefinitions>
    
    <!-- 메인 이미지 -->
    <!-- Main image -->
    <Image Source="{Binding SelectedImage}" 
           Stretch="Uniform" 
           Grid.Row="0"/>
    
    <!-- 네비게이션 버튼 -->
    <!-- Navigation buttons -->
    <StackPanel Grid.Row="1" Orientation="Horizontal" 
                HorizontalAlignment="Center" Margin="10">
        <Button Content="◀ 이전" Command="{Binding PreviousImageCommand}" Margin="5"/>
        <Button Content="다음 ▶" Command="{Binding NextImageCommand}" Margin="5"/>
    </StackPanel>
    
    <!-- 썸네일 목록 -->
    <!-- Thumbnail list -->
    <ListBox Grid.Row="2" 
             ItemsSource="{Binding Images}"
             SelectedIndex="{Binding SelectedIndex}"
             SelectedItem="{Binding SelectedImage}">
        <ListBox.ItemsPanel>
            <ItemsPanelTemplate>
                <VirtualizingStackPanel Orientation="Horizontal"/>
            </ItemsPanelTemplate>
        </ListBox.ItemsPanel>
        <ListBox.ItemTemplate>
            <DataTemplate>
                <Image Source="{Binding}" Width="80" Height="60" 
                       Stretch="UniformToFill" Margin="2"/>
            </DataTemplate>
        </ListBox.ItemTemplate>
    </ListBox>
</Grid>
```

---

## 5. 성능 최적화

### 5.1 이미지 메모리 관리

```csharp
// DecodePixelWidth/Height로 메모리 절약
// Save memory with DecodePixelWidth/Height
bitmap.DecodePixelWidth = 200; // 원본 대신 200px로 디코딩
                                // Decode at 200px instead of original

// Freeze()로 성능 향상
// Improve performance with Freeze()
bitmap.Freeze();

// 대용량 이미지 지연 로딩
// Lazy loading for large images
bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
```

### 5.2 비디오 성능

```csharp
// 하드웨어 가속
// Hardware acceleration
RenderOptions.ProcessRenderMode = RenderMode.Default;

// 비디오 메모리 해제
// Release video memory
videoPlayer.Close();
videoPlayer.Source = null;
```

---

## 6. 참고 문서

- [Image Class - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.image)
- [MediaElement Class - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.mediaelement)
- [Multimedia Overview - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/multimedia-overview)
