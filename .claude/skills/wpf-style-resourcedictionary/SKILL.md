---
name: wpf-style-resourcedictionary
description: WPF Style 정의와 ResourceDictionary 관리 패턴. Style 상속(BasedOn), ResourceDictionary 병합, StaticResource vs DynamicResource 선택 기준. 테마 시스템 구축 및 리소스 관리 시 이 스킬 적용.
---

# WPF Style & ResourceDictionary 패턴

Style과 ResourceDictionary를 효과적으로 관리하여 일관된 UI와 유지보수성을 확보합니다.

## 1. Style 기본 구조

### 1.1 명시적 Style (키 지정)

```xml
<Window.Resources>
    <!-- 명시적 스타일: 키로 참조해야 적용 -->
    <!-- Explicit style: must reference by key to apply -->
    <Style x:Key="PrimaryButtonStyle" TargetType="{x:Type Button}">
        <Setter Property="Background" Value="#2196F3"/>
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="Padding" Value="16,8"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
    </Style>
</Window.Resources>

<Button Style="{StaticResource PrimaryButtonStyle}" Content="Primary"/>
```

### 1.2 암시적 Style (키 생략)

```xml
<Window.Resources>
    <!-- 암시적 스타일: 해당 타입 모든 컨트롤에 자동 적용 -->
    <!-- Implicit style: auto-applied to all controls of that type -->
    <Style TargetType="{x:Type Button}">
        <Setter Property="Margin" Value="5"/>
        <Setter Property="Cursor" Value="Hand"/>
    </Style>
</Window.Resources>

<!-- 자동으로 스타일 적용됨 -->
<!-- Style automatically applied -->
<Button Content="Auto Styled"/>
```

---

## 2. Style 상속 (BasedOn)

### 2.1 기본 상속

```xml
<!-- 기본 버튼 스타일 -->
<!-- Base button style -->
<Style x:Key="BaseButtonStyle" TargetType="{x:Type Button}">
    <Setter Property="Padding" Value="12,6"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Cursor" Value="Hand"/>
</Style>

<!-- Primary 버튼: 기본 스타일 상속 -->
<!-- Primary button: inherits base style -->
<Style x:Key="PrimaryButtonStyle" TargetType="{x:Type Button}"
       BasedOn="{StaticResource BaseButtonStyle}">
    <Setter Property="Background" Value="#2196F3"/>
    <Setter Property="Foreground" Value="White"/>
</Style>

<!-- Secondary 버튼: 기본 스타일 상속 -->
<!-- Secondary button: inherits base style -->
<Style x:Key="SecondaryButtonStyle" TargetType="{x:Type Button}"
       BasedOn="{StaticResource BaseButtonStyle}">
    <Setter Property="Background" Value="#E0E0E0"/>
    <Setter Property="Foreground" Value="#424242"/>
</Style>
```

### 2.2 암시적 스타일 상속

```xml
<!-- 암시적 스타일을 상속받는 명시적 스타일 -->
<!-- Explicit style inheriting from implicit style -->
<Style TargetType="{x:Type Button}">
    <Setter Property="Margin" Value="5"/>
</Style>

<Style x:Key="SpecialButtonStyle" TargetType="{x:Type Button}"
       BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="Background" Value="Gold"/>
</Style>
```

---

## 3. ResourceDictionary

### 3.1 파일 구조

```
📁 Themes/
├── 📄 Colors.xaml          (색상 정의)
├── 📄 Brushes.xaml         (브러시 정의)
├── 📄 Converters.xaml      (컨버터 정의)
├── 📄 Controls/
│   ├── 📄 Button.xaml      (버튼 스타일)
│   ├── 📄 TextBox.xaml     (텍스트박스 스타일)
│   └── 📄 ListBox.xaml     (리스트박스 스타일)
└── 📄 Generic.xaml         (병합 딕셔너리)
```

### 3.2 Colors.xaml

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <!-- 기본 색상 팔레트 -->
    <!-- Base color palette -->
    <Color x:Key="PrimaryColor">#2196F3</Color>
    <Color x:Key="PrimaryDarkColor">#1976D2</Color>
    <Color x:Key="PrimaryLightColor">#BBDEFB</Color>
    
    <Color x:Key="AccentColor">#FF4081</Color>
    
    <Color x:Key="TextPrimaryColor">#212121</Color>
    <Color x:Key="TextSecondaryColor">#757575</Color>
    
    <Color x:Key="BackgroundColor">#FAFAFA</Color>
    <Color x:Key="SurfaceColor">#FFFFFF</Color>
    
    <Color x:Key="ErrorColor">#F44336</Color>
    <Color x:Key="SuccessColor">#4CAF50</Color>
    <Color x:Key="WarningColor">#FFC107</Color>
    
</ResourceDictionary>
```

### 3.3 Brushes.xaml

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Colors.xaml 병합 -->
    <!-- Merge Colors.xaml -->
    <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="Colors.xaml"/>
    </ResourceDictionary.MergedDictionaries>

    <!-- SolidColorBrush 정의 -->
    <!-- SolidColorBrush definitions -->
    <SolidColorBrush x:Key="PrimaryBrush" Color="{StaticResource PrimaryColor}"/>
    <SolidColorBrush x:Key="PrimaryDarkBrush" Color="{StaticResource PrimaryDarkColor}"/>
    <SolidColorBrush x:Key="PrimaryLightBrush" Color="{StaticResource PrimaryLightColor}"/>
    
    <SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}"/>
    
    <SolidColorBrush x:Key="TextPrimaryBrush" Color="{StaticResource TextPrimaryColor}"/>
    <SolidColorBrush x:Key="TextSecondaryBrush" Color="{StaticResource TextSecondaryColor}"/>
    
    <SolidColorBrush x:Key="BackgroundBrush" Color="{StaticResource BackgroundColor}"/>
    <SolidColorBrush x:Key="SurfaceBrush" Color="{StaticResource SurfaceColor}"/>

</ResourceDictionary>
```

### 3.4 Generic.xaml (병합 딕셔너리)

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <ResourceDictionary.MergedDictionaries>
        <!-- 순서 중요: 의존성 순서대로 병합 -->
        <!-- Order matters: merge in dependency order -->
        <ResourceDictionary Source="Colors.xaml"/>
        <ResourceDictionary Source="Brushes.xaml"/>
        <ResourceDictionary Source="Converters.xaml"/>
        <ResourceDictionary Source="Controls/Button.xaml"/>
        <ResourceDictionary Source="Controls/TextBox.xaml"/>
        <ResourceDictionary Source="Controls/ListBox.xaml"/>
    </ResourceDictionary.MergedDictionaries>

</ResourceDictionary>
```

### 3.5 App.xaml에서 로드

```xml
<Application x:Class="MyApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="/Themes/Generic.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

---

## 4. StaticResource vs DynamicResource

### 4.1 비교

| 구분 | StaticResource | DynamicResource |
|------|----------------|-----------------|
| **평가 시점** | XAML 로드 시 1회 | 런타임에 매번 |
| **성능** | 빠름 | 상대적으로 느림 |
| **변경 반영** | 안됨 | 자동 반영 |
| **forward reference** | 불가 | 가능 |
| **용도** | 고정 리소스 | 테마 변경, 동적 리소스 |

### 4.2 사용 예시

```xml
<!-- StaticResource: 변경되지 않는 리소스 -->
<!-- StaticResource: immutable resources -->
<Button Background="{StaticResource PrimaryBrush}"/>

<!-- DynamicResource: 런타임 변경 필요 시 -->
<!-- DynamicResource: when runtime changes needed -->
<Border Background="{DynamicResource ThemeBackgroundBrush}"/>
```

### 4.3 테마 전환 구현

```csharp
namespace MyApp.Services;

using System;
using System.Windows;

public sealed class ThemeService
{
    private const string LightThemePath = "/Themes/LightTheme.xaml";
    private const string DarkThemePath = "/Themes/DarkTheme.xaml";

    /// <summary>
    /// 테마 전환
    /// Switch theme
    /// </summary>
    public void SwitchTheme(bool isDark)
    {
        var themePath = isDark ? DarkThemePath : LightThemePath;
        var themeUri = new Uri(themePath, UriKind.Relative);
        
        var app = Application.Current;
        var mergedDicts = app.Resources.MergedDictionaries;
        
        // 기존 테마 제거
        // Remove existing theme
        for (var i = mergedDicts.Count - 1; i >= 0; i--)
        {
            var dict = mergedDicts[i];
            if (dict.Source?.OriginalString.Contains("Theme") == true)
            {
                mergedDicts.RemoveAt(i);
            }
        }
        
        // 새 테마 추가
        // Add new theme
        mergedDicts.Add(new ResourceDictionary { Source = themeUri });
    }
}
```

---

## 5. 코드에서 리소스 접근

### 5.1 리소스 검색

```csharp
namespace MyApp.Helpers;

using System.Windows;
using System.Windows.Media;

public static class ResourceHelper
{
    /// <summary>
    /// 리소스 검색 (FindResource - 없으면 예외)
    /// Find resource (FindResource - throws if not found)
    /// </summary>
    public static Brush GetBrush(string key)
    {
        return (Brush)Application.Current.FindResource(key);
    }

    /// <summary>
    /// 리소스 검색 (TryFindResource - 없으면 null)
    /// Find resource (TryFindResource - returns null if not found)
    /// </summary>
    public static Brush? TryGetBrush(string key)
    {
        return Application.Current.TryFindResource(key) as Brush;
    }

    /// <summary>
    /// 요소 기준 리소스 검색 (상위로 탐색)
    /// Find resource from element (searches upward)
    /// </summary>
    public static T? FindResource<T>(FrameworkElement element, string key) where T : class
    {
        return element.TryFindResource(key) as T;
    }
}
```

### 5.2 동적 리소스 설정

```csharp
// DynamicResource를 코드에서 설정
// Set DynamicResource from code
button.SetResourceReference(Button.BackgroundProperty, "PrimaryBrush");

// StaticResource를 코드에서 설정 (리소스 직접 할당)
// Set StaticResource from code (direct resource assignment)
button.Background = (Brush)FindResource("PrimaryBrush");
```

---

## 6. ComponentResourceKey (외부 라이브러리용)

### 6.1 정의

```csharp
namespace MyLib.Controls;

using System.Windows;

public static class MyLibResources
{
    // 컴포넌트 리소스 키 정의
    // Define component resource key
    public static readonly ComponentResourceKey PrimaryBrushKey =
        new(typeof(MyLibResources), "PrimaryBrush");
    
    public static readonly ComponentResourceKey ButtonStyleKey =
        new(typeof(MyLibResources), "ButtonStyle");
}
```

### 6.2 사용

```xml
<!-- Generic.xaml (Themes 폴더) -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:local="clr-namespace:MyLib.Controls">

    <SolidColorBrush x:Key="{ComponentResourceKey TypeInTargetAssembly={x:Type local:MyLibResources}, ResourceId=PrimaryBrush}"
                     Color="#2196F3"/>

</ResourceDictionary>
```

```xml
<!-- 사용 측 -->
<!-- Consumer side -->
<Button Background="{StaticResource {x:Static local:MyLibResources.PrimaryBrushKey}}"/>
```

---

## 7. 리소스 참조 순서

```
1. 요소 자체 Resources
2. 부모 요소 Resources (Visual Tree 상향 탐색)
3. Window/Page Resources
4. Application.Resources
5. 테마 리소스 (Generic.xaml)
6. 시스템 리소스 (SystemColors, SystemFonts)
```

---

## 8. 체크리스트

- [ ] 색상은 Color 타입으로 정의, Brush는 별도 파일
- [ ] 컨트롤별 스타일 파일 분리
- [ ] 고정 리소스는 StaticResource, 테마 리소스는 DynamicResource
- [ ] ResourceDictionary 병합 순서 확인 (의존성 순)
- [ ] 공통 스타일은 BasedOn으로 상속
- [ ] ComponentResourceKey로 라이브러리 리소스 노출

---

## 9. 참고 문서

- [Resources Overview - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/systems/xaml-resources-overview)
- [Styles and Templates - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/styles-templates-overview)
