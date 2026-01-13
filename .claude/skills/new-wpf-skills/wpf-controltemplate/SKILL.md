---
name: wpf-controltemplate
description: WPF ControlTemplate을 사용한 컨트롤 외관 커스터마이징. Control 클래스를 상속받은 컨트롤의 시각적 구조 재정의, TemplateBinding, ContentPresenter, Trigger 패턴. (1) 기존 컨트롤 외관 완전 변경, (2) 상태별 시각적 피드백 구현, (3) TemplatedParent 바인딩 시 이 스킬 적용.
---

# WPF ControlTemplate 패턴

Control 클래스에서 상속된 모든 컨트롤은 ControlTemplate을 통해 시각적 구조를 완전히 재정의할 수 있습니다.

## 1. 핵심 개념

### ControlTemplate vs Style

| 구분 | Style | ControlTemplate |
|------|-------|-----------------|
| **역할** | 속성 값 일괄 설정 | 시각적 구조 재정의 |
| **범위** | 속성 변경만 가능 | 전체 외관 변경 가능 |
| **적용 대상** | 모든 FrameworkElement | Control 상속 클래스만 |

### ControlTemplate 구성 요소

- **TargetType**: 템플릿이 적용될 컨트롤 타입
- **TemplateBinding**: TemplatedParent 속성 연결
- **ContentPresenter**: Content 속성 렌더링 위치 지정
- **Triggers**: 상태별 시각적 변경

---

## 2. 기본 구현 패턴

### 2.1 Button ControlTemplate (XAML)

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Style TargetType="{x:Type Button}" x:Key="RoundedButtonStyle">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type Button}">
                    <!-- 시각적 구조 정의 -->
                    <!-- Visual structure definition -->
                    <Border x:Name="PART_Border"
                            CornerRadius="10"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            Background="{TemplateBinding Background}"
                            Padding="{TemplateBinding Padding}">
                        
                        <!-- Content 렌더링 위치 -->
                        <!-- Content rendering location -->
                        <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                          VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                                          RecognizesAccessKey="True"/>
                    </Border>
                    
                    <!-- 상태별 트리거 -->
                    <!-- State-based triggers -->
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="PART_Border" Property="Background" Value="#E3F2FD"/>
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="PART_Border" Property="Background" Value="#BBDEFB"/>
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="PART_Border" Property="Opacity" Value="0.5"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        
        <!-- 기본 속성 값 -->
        <!-- Default property values -->
        <Setter Property="Background" Value="#2196F3"/>
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Padding" Value="16,8"/>
        <Setter Property="Cursor" Value="Hand"/>
    </Style>

</ResourceDictionary>
```

### 2.2 코드에서 ControlTemplate 적용

```csharp
namespace MyApp.Helpers;

using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

public static class TemplateHelper
{
    /// <summary>
    /// XAML 문자열로부터 ControlTemplate 생성
    /// Create ControlTemplate from XAML string
    /// </summary>
    public static ControlTemplate CreateTemplate(string xaml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
        
        var context = new ParserContext();
        context.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
        context.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
        
        return (ControlTemplate)XamlReader.Load(stream, context);
    }
}
```

---

## 3. TemplateBinding vs Binding

### 3.1 TemplateBinding (권장)

```xml
<!-- 컴파일 타임 바인딩, 단방향, 성능 우수 -->
<!-- Compile-time binding, one-way, better performance -->
<Border Background="{TemplateBinding Background}"/>
```

### 3.2 RelativeSource TemplatedParent (양방향 필요 시)

```xml
<!-- 런타임 바인딩, 양방향 가능, 상대적으로 느림 -->
<!-- Runtime binding, two-way possible, relatively slower -->
<Border Background="{Binding Background, RelativeSource={RelativeSource TemplatedParent}}"/>
```

### 비교

| 구분 | TemplateBinding | RelativeSource TemplatedParent |
|------|-----------------|-------------------------------|
| **방향** | 단방향 (OneWay) | 양방향 가능 |
| **성능** | 빠름 | 상대적으로 느림 |
| **Converter** | 사용 불가 | 사용 가능 |
| **용도** | 대부분의 경우 | 양방향/컨버터 필요 시 |

---

## 4. ContentPresenter 상세

### 4.1 주요 속성

```xml
<ContentPresenter 
    Content="{TemplateBinding Content}"
    ContentTemplate="{TemplateBinding ContentTemplate}"
    ContentTemplateSelector="{TemplateBinding ContentTemplateSelector}"
    ContentStringFormat="{TemplateBinding ContentStringFormat}"
    HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
    VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
    Margin="{TemplateBinding Padding}"
    RecognizesAccessKey="True"/>
```

### 4.2 RecognizesAccessKey

```xml
<!-- Alt + 밑줄 문자로 버튼 활성화 가능 -->
<!-- Enable button activation with Alt + underlined character -->
<Button Content="_Save"/>  <!-- Alt+S로 활성화 -->
```

---

## 5. Trigger 패턴

### 5.1 Property Trigger

```xml
<ControlTemplate.Triggers>
    <!-- 단일 속성 조건 -->
    <!-- Single property condition -->
    <Trigger Property="IsMouseOver" Value="True">
        <Setter TargetName="PART_Border" Property="Background" Value="LightBlue"/>
    </Trigger>
</ControlTemplate.Triggers>
```

### 5.2 MultiTrigger

```xml
<ControlTemplate.Triggers>
    <!-- 복수 속성 조건 (AND) -->
    <!-- Multiple property conditions (AND) -->
    <MultiTrigger>
        <MultiTrigger.Conditions>
            <Condition Property="IsMouseOver" Value="True"/>
            <Condition Property="IsEnabled" Value="True"/>
        </MultiTrigger.Conditions>
        <Setter TargetName="PART_Border" Property="Background" Value="LightGreen"/>
    </MultiTrigger>
</ControlTemplate.Triggers>
```

### 5.3 EventTrigger (애니메이션)

```xml
<ControlTemplate.Triggers>
    <EventTrigger RoutedEvent="MouseEnter">
        <BeginStoryboard>
            <Storyboard>
                <DoubleAnimation Storyboard.TargetName="PART_Border"
                                 Storyboard.TargetProperty="Opacity"
                                 To="0.8" Duration="0:0:0.2"/>
            </Storyboard>
        </BeginStoryboard>
    </EventTrigger>
</ControlTemplate.Triggers>
```

---

## 6. 실전 예제: 토글 버튼

```xml
<Style TargetType="{x:Type ToggleButton}" x:Key="SwitchToggleStyle">
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="{x:Type ToggleButton}">
                <Grid>
                    <!-- 배경 트랙 -->
                    <!-- Background track -->
                    <Border x:Name="PART_Track"
                            Width="50" Height="26"
                            CornerRadius="13"
                            Background="#E0E0E0"/>
                    
                    <!-- 슬라이딩 썸 -->
                    <!-- Sliding thumb -->
                    <Border x:Name="PART_Thumb"
                            Width="22" Height="22"
                            CornerRadius="11"
                            Background="White"
                            HorizontalAlignment="Left"
                            Margin="2,0,0,0">
                        <Border.Effect>
                            <DropShadowEffect ShadowDepth="1" BlurRadius="3" Opacity="0.3"/>
                        </Border.Effect>
                    </Border>
                </Grid>
                
                <ControlTemplate.Triggers>
                    <!-- 체크됨 상태 -->
                    <!-- Checked state -->
                    <Trigger Property="IsChecked" Value="True">
                        <Setter TargetName="PART_Track" Property="Background" Value="#4CAF50"/>
                        <Setter TargetName="PART_Thumb" Property="HorizontalAlignment" Value="Right"/>
                        <Setter TargetName="PART_Thumb" Property="Margin" Value="0,0,2,0"/>
                    </Trigger>
                    
                    <!-- 비활성화 상태 -->
                    <!-- Disabled state -->
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter Property="Opacity" Value="0.5"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

---

## 7. PART_ 네이밍 컨벤션

CustomControl에서 코드 비하인드가 특정 요소에 접근해야 할 때 사용:

```xml
<!-- PART_ 접두사로 필수 요소 표시 -->
<!-- Mark required elements with PART_ prefix -->
<Border x:Name="PART_Border"/>
<ContentPresenter x:Name="PART_ContentHost"/>
<Popup x:Name="PART_Popup"/>
```

```csharp
// OnApplyTemplate에서 PART_ 요소 검색
// Find PART_ elements in OnApplyTemplate
public override void OnApplyTemplate()
{
    base.OnApplyTemplate();
    
    var border = GetTemplateChild("PART_Border") as Border;
    var popup = GetTemplateChild("PART_Popup") as Popup;
}
```

---

## 8. 체크리스트

- [ ] TargetType 명시
- [ ] TemplateBinding으로 속성 연결
- [ ] ContentPresenter에 RecognizesAccessKey="True" 설정
- [ ] IsMouseOver, IsPressed, IsEnabled, IsFocused 트리거 처리
- [ ] PART_ 네이밍으로 필수 요소 표시
- [ ] 기본 속성 값을 Style의 Setter로 지정

---

## 9. 참고 문서

- [ControlTemplate - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/controltemplate)
- [Styling and Templating - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/styles-templates-overview)
