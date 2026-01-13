---
name: wpf-animation
description: WPF 애니메이션 시스템. Storyboard, Timeline, EasingFunction 패턴. DoubleAnimation, ColorAnimation, ObjectAnimationUsingKeyFrames 활용. UI 전환, 상태 변화 시각화, 인터랙티브 피드백 구현 시 이 스킬 적용.
---

# WPF Animation 패턴

WPF 애니메이션은 시간 기반으로 속성 값을 변경하여 시각적 효과를 만듭니다.

## 1. 애니메이션 구성 요소

```
Storyboard (컨테이너)
├── Timeline (시간 제어)
│   ├── Animation (값 변경)
│   │   ├── DoubleAnimation
│   │   ├── ColorAnimation
│   │   └── ...
│   └── AnimationUsingKeyFrames (키프레임)
│       ├── DoubleAnimationUsingKeyFrames
│       └── ...
└── EasingFunction (가속/감속)
```

---

## 2. 기본 애니메이션 (XAML)

### 2.1 DoubleAnimation

```xml
<Button Content="Hover Me" Width="100">
    <Button.Style>
        <Style TargetType="Button">
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Trigger.EnterActions>
                        <BeginStoryboard>
                            <Storyboard>
                                <!-- Width 애니메이션 -->
                                <!-- Width animation -->
                                <DoubleAnimation 
                                    Storyboard.TargetProperty="Width"
                                    To="150"
                                    Duration="0:0:0.3"/>
                            </Storyboard>
                        </BeginStoryboard>
                    </Trigger.EnterActions>
                    <Trigger.ExitActions>
                        <BeginStoryboard>
                            <Storyboard>
                                <DoubleAnimation 
                                    Storyboard.TargetProperty="Width"
                                    To="100"
                                    Duration="0:0:0.3"/>
                            </Storyboard>
                        </BeginStoryboard>
                    </Trigger.ExitActions>
                </Trigger>
            </Style.Triggers>
        </Style>
    </Button.Style>
</Button>
```

### 2.2 ColorAnimation

```xml
<Border x:Name="AnimatedBorder" Background="Blue" Width="100" Height="100">
    <Border.Triggers>
        <EventTrigger RoutedEvent="MouseEnter">
            <BeginStoryboard>
                <Storyboard>
                    <!-- 배경색 애니메이션 -->
                    <!-- Background color animation -->
                    <ColorAnimation 
                        Storyboard.TargetProperty="(Border.Background).(SolidColorBrush.Color)"
                        To="Red"
                        Duration="0:0:0.5"/>
                </Storyboard>
            </BeginStoryboard>
        </EventTrigger>
        <EventTrigger RoutedEvent="MouseLeave">
            <BeginStoryboard>
                <Storyboard>
                    <ColorAnimation 
                        Storyboard.TargetProperty="(Border.Background).(SolidColorBrush.Color)"
                        To="Blue"
                        Duration="0:0:0.5"/>
                </Storyboard>
            </BeginStoryboard>
        </EventTrigger>
    </Border.Triggers>
</Border>
```

### 2.3 ThicknessAnimation (Margin, Padding)

```xml
<Border x:Name="SlidingBorder" Margin="0,0,0,0">
    <Border.Triggers>
        <EventTrigger RoutedEvent="Loaded">
            <BeginStoryboard>
                <Storyboard>
                    <!-- 슬라이드 인 효과 -->
                    <!-- Slide-in effect -->
                    <ThicknessAnimation 
                        Storyboard.TargetProperty="Margin"
                        From="-100,0,0,0"
                        To="0,0,0,0"
                        Duration="0:0:0.5"/>
                </Storyboard>
            </BeginStoryboard>
        </EventTrigger>
    </Border.Triggers>
</Border>
```

---

## 3. EasingFunction

### 3.1 주요 Easing 종류

```xml
<Storyboard>
    <DoubleAnimation Storyboard.TargetProperty="Width" To="200" Duration="0:0:0.5">
        <DoubleAnimation.EasingFunction>
            <!-- 다양한 이징 함수 -->
            <!-- Various easing functions -->
            
            <!-- 부드러운 감속 -->
            <!-- Smooth deceleration -->
            <QuadraticEase EasingMode="EaseOut"/>
            
            <!-- 탄성 효과 -->
            <!-- Elastic effect -->
            <!--<ElasticEase Oscillations="3" Springiness="5"/>-->
            
            <!-- 바운스 효과 -->
            <!-- Bounce effect -->
            <!--<BounceEase Bounces="3" Bounciness="2"/>-->
            
            <!-- 백 효과 (오버슛) -->
            <!-- Back effect (overshoot) -->
            <!--<BackEase Amplitude="0.3"/>-->
            
            <!-- 원형 이징 -->
            <!-- Circular easing -->
            <!--<CircleEase EasingMode="EaseInOut"/>-->
        </DoubleAnimation.EasingFunction>
    </DoubleAnimation>
</Storyboard>
```

### 3.2 EasingMode

| 모드 | 설명 |
|------|------|
| **EaseIn** | 시작 시 느림, 끝에서 빠름 |
| **EaseOut** | 시작 시 빠름, 끝에서 느림 |
| **EaseInOut** | 양쪽 끝에서 느림, 중간에서 빠름 |

---

## 4. KeyFrame 애니메이션

### 4.1 DoubleAnimationUsingKeyFrames

```xml
<Storyboard>
    <DoubleAnimationUsingKeyFrames Storyboard.TargetProperty="Opacity">
        <!-- 순차적 키프레임 -->
        <!-- Sequential keyframes -->
        <LinearDoubleKeyFrame Value="0.3" KeyTime="0:0:0.2"/>
        <LinearDoubleKeyFrame Value="1.0" KeyTime="0:0:0.4"/>
        <SplineDoubleKeyFrame Value="0.5" KeyTime="0:0:0.8">
            <SplineDoubleKeyFrame.KeySpline>
                <KeySpline ControlPoint1="0.5,0" ControlPoint2="0.5,1"/>
            </SplineDoubleKeyFrame.KeySpline>
        </SplineDoubleKeyFrame>
        <EasingDoubleKeyFrame Value="1.0" KeyTime="0:0:1">
            <EasingDoubleKeyFrame.EasingFunction>
                <BounceEase/>
            </EasingDoubleKeyFrame.EasingFunction>
        </EasingDoubleKeyFrame>
    </DoubleAnimationUsingKeyFrames>
</Storyboard>
```

### 4.2 ObjectAnimationUsingKeyFrames (불연속 값)

```xml
<Storyboard>
    <!-- Visibility 전환 (연속값이 아닌 경우) -->
    <!-- Visibility toggle (non-continuous values) -->
    <ObjectAnimationUsingKeyFrames Storyboard.TargetProperty="Visibility">
        <DiscreteObjectKeyFrame KeyTime="0:0:0" Value="{x:Static Visibility.Visible}"/>
        <DiscreteObjectKeyFrame KeyTime="0:0:1" Value="{x:Static Visibility.Hidden}"/>
        <DiscreteObjectKeyFrame KeyTime="0:0:2" Value="{x:Static Visibility.Visible}"/>
    </ObjectAnimationUsingKeyFrames>
</Storyboard>
```

---

## 5. Transform 애니메이션

### 5.1 RenderTransform 애니메이션

```xml
<Button Content="Rotate" RenderTransformOrigin="0.5,0.5">
    <Button.RenderTransform>
        <RotateTransform x:Name="RotateTransform" Angle="0"/>
    </Button.RenderTransform>
    <Button.Triggers>
        <EventTrigger RoutedEvent="Click">
            <BeginStoryboard>
                <Storyboard>
                    <!-- 회전 애니메이션 -->
                    <!-- Rotation animation -->
                    <DoubleAnimation 
                        Storyboard.TargetName="RotateTransform"
                        Storyboard.TargetProperty="Angle"
                        By="360"
                        Duration="0:0:0.5"/>
                </Storyboard>
            </BeginStoryboard>
        </EventTrigger>
    </Button.Triggers>
</Button>
```

### 5.2 복합 Transform 애니메이션

```xml
<Border Width="100" Height="100" Background="Blue" RenderTransformOrigin="0.5,0.5">
    <Border.RenderTransform>
        <TransformGroup>
            <ScaleTransform x:Name="Scale" ScaleX="1" ScaleY="1"/>
            <RotateTransform x:Name="Rotate" Angle="0"/>
            <TranslateTransform x:Name="Translate" X="0" Y="0"/>
        </TransformGroup>
    </Border.RenderTransform>
    <Border.Triggers>
        <EventTrigger RoutedEvent="MouseEnter">
            <BeginStoryboard>
                <Storyboard>
                    <!-- 동시 실행 애니메이션 -->
                    <!-- Simultaneous animations -->
                    <DoubleAnimation Storyboard.TargetName="Scale" 
                                     Storyboard.TargetProperty="ScaleX" 
                                     To="1.2" Duration="0:0:0.2"/>
                    <DoubleAnimation Storyboard.TargetName="Scale" 
                                     Storyboard.TargetProperty="ScaleY" 
                                     To="1.2" Duration="0:0:0.2"/>
                    <DoubleAnimation Storyboard.TargetName="Rotate" 
                                     Storyboard.TargetProperty="Angle" 
                                     To="10" Duration="0:0:0.2"/>
                </Storyboard>
            </BeginStoryboard>
        </EventTrigger>
    </Border.Triggers>
</Border>
```

---

## 6. 코드에서 애니메이션

### 6.1 기본 애니메이션

```csharp
namespace MyApp.Animations;

using System;
using System.Windows;
using System.Windows.Media.Animation;

public static class AnimationHelper
{
    /// <summary>
    /// Opacity 페이드 애니메이션
    /// Opacity fade animation
    /// </summary>
    public static void FadeIn(UIElement element, double durationSeconds = 0.3)
    {
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromSeconds(durationSeconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    public static void FadeOut(UIElement element, double durationSeconds = 0.3, Action? onCompleted = null)
    {
        var animation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromSeconds(durationSeconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        if (onCompleted is not null)
        {
            animation.Completed += (s, e) => onCompleted();
        }
        
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }
}
```

### 6.2 Storyboard로 복합 애니메이션

```csharp
namespace MyApp.Animations;

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

public static class StoryboardHelper
{
    /// <summary>
    /// 확대 + 페이드 인 애니메이션
    /// Scale up + fade in animation
    /// </summary>
    public static void ScaleFadeIn(FrameworkElement element, double durationSeconds = 0.3)
    {
        // Transform 설정
        // Setup transform
        var scaleTransform = new ScaleTransform(0.8, 0.8);
        element.RenderTransform = scaleTransform;
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.Opacity = 0;

        var storyboard = new Storyboard();

        // Opacity 애니메이션
        // Opacity animation
        var opacityAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromSeconds(durationSeconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(opacityAnimation, element);
        Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(opacityAnimation);

        // ScaleX 애니메이션
        // ScaleX animation
        var scaleXAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromSeconds(durationSeconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(scaleXAnimation, element);
        Storyboard.SetTargetProperty(scaleXAnimation, 
            new PropertyPath("RenderTransform.ScaleX"));
        storyboard.Children.Add(scaleXAnimation);

        // ScaleY 애니메이션
        // ScaleY animation
        var scaleYAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromSeconds(durationSeconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(scaleYAnimation, element);
        Storyboard.SetTargetProperty(scaleYAnimation, 
            new PropertyPath("RenderTransform.ScaleY"));
        storyboard.Children.Add(scaleYAnimation);

        storyboard.Begin();
    }
}
```

### 6.3 애니메이션 중단/재개

```csharp
namespace MyApp.Animations;

using System.Windows;
using System.Windows.Media.Animation;

public sealed class AnimationController
{
    private readonly Storyboard _storyboard;
    private readonly FrameworkElement _target;

    public AnimationController(Storyboard storyboard, FrameworkElement target)
    {
        _storyboard = storyboard;
        _target = target;
    }

    public void Start()
    {
        _storyboard.Begin(_target, isControllable: true);
    }

    public void Pause()
    {
        _storyboard.Pause(_target);
    }

    public void Resume()
    {
        _storyboard.Resume(_target);
    }

    public void Stop()
    {
        _storyboard.Stop(_target);
    }

    public void Seek(TimeSpan offset)
    {
        _storyboard.Seek(_target, offset, TimeSeekOrigin.BeginTime);
    }
}
```

---

## 7. VisualStateManager 통합

### 7.1 상태 기반 애니메이션

```xml
<Style TargetType="{x:Type Button}">
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="{x:Type Button}">
                <Border x:Name="Border" Background="{TemplateBinding Background}">
                    <VisualStateManager.VisualStateGroups>
                        <VisualStateGroup x:Name="CommonStates">
                            <VisualState x:Name="Normal">
                                <Storyboard>
                                    <ColorAnimation 
                                        Storyboard.TargetName="Border"
                                        Storyboard.TargetProperty="(Border.Background).(SolidColorBrush.Color)"
                                        To="#2196F3" Duration="0:0:0.2"/>
                                </Storyboard>
                            </VisualState>
                            <VisualState x:Name="MouseOver">
                                <Storyboard>
                                    <ColorAnimation 
                                        Storyboard.TargetName="Border"
                                        Storyboard.TargetProperty="(Border.Background).(SolidColorBrush.Color)"
                                        To="#1976D2" Duration="0:0:0.2"/>
                                </Storyboard>
                            </VisualState>
                            <VisualState x:Name="Pressed">
                                <Storyboard>
                                    <ColorAnimation 
                                        Storyboard.TargetName="Border"
                                        Storyboard.TargetProperty="(Border.Background).(SolidColorBrush.Color)"
                                        To="#0D47A1" Duration="0:0:0.1"/>
                                </Storyboard>
                            </VisualState>
                        </VisualStateGroup>
                    </VisualStateManager.VisualStateGroups>
                    <ContentPresenter/>
                </Border>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

---

## 8. 성능 고려사항

| 속성 | 성능 | 설명 |
|------|------|------|
| **Opacity** | ⭐⭐⭐ | 가장 효율적 |
| **RenderTransform** | ⭐⭐⭐ | 레이아웃 재계산 없음 |
| **Clip** | ⭐⭐ | 중간 성능 |
| **Width/Height** | ⭐ | 레이아웃 재계산 발생 |
| **Margin** | ⭐ | 레이아웃 재계산 발생 |

```csharp
// 성능 최적화 힌트
// Performance optimization hints
RenderOptions.SetBitmapScalingMode(element, BitmapScalingMode.LowQuality);
Timeline.SetDesiredFrameRate(storyboard, 30); // 기본 60fps → 30fps
```

---

## 9. 참고 문서

- [Animation Overview - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/animation-overview)
- [Storyboards Overview - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/storyboards-overview)
