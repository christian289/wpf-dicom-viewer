---
name: wpf-command-parameter-enum
description: 'WPF CommandParameter에 enum 값 바인딩 시 x:Static 사용 패턴'
---

# WPF Command Parameter Enum 타입 바인딩

## 문제 상황

WPF에서 `CommandParameter`에 enum 값을 바인딩할 때, **문자열로 전달하면 타입 불일치 오류**가 발생합니다.

### 에러 메시지
```
System.ArgumentException: 'Parameter "parameter" (object) cannot be of type System.String,
as the command type requires an argument of type MyNamespace.MyEnum.'
```

### 원인
XAML에서 `CommandParameter="Pan"`처럼 문자열로 지정하면, WPF는 이를 `System.String` 타입으로 전달합니다. 그러나 Command가 특정 enum 타입을 기대하는 경우 타입 변환이 자동으로 이루어지지 않습니다.

---

## 해결 방법

### `x:Static`을 사용하여 enum 값 직접 참조

```xml
<!-- 네임스페이스 선언 -->
xmlns:viewmodels="clr-namespace:MyApp.ViewModels;assembly=MyApp.ViewModels"

<!-- 잘못된 방법 (String으로 전달됨) -->
<Button Command="{Binding SelectToolCommand}"
        CommandParameter="Pan" />

<!-- 올바른 방법 (Enum 타입으로 전달됨) -->
<Button Command="{Binding SelectToolCommand}"
        CommandParameter="{x:Static viewmodels:ViewerTool.Pan}" />
```

---

## 전체 예시

### ViewModel (C#)
```csharp
public enum ViewerTool
{
    None,
    Pan,
    Zoom,
    WindowLevel
}

public partial class ViewerViewModel : ObservableObject
{
    [ObservableProperty]
    private ViewerTool _currentTool = ViewerTool.Pan;

    [RelayCommand]
    private void SelectTool(ViewerTool tool)
    {
        CurrentTool = tool;
    }
}
```

### View (XAML)
```xml
<UserControl xmlns:viewmodels="clr-namespace:MyApp.ViewModels;assembly=MyApp.ViewModels">

    <StackPanel Orientation="Horizontal">
        <!-- Pan 도구 선택 -->
        <ToggleButton Content="Pan"
                      Command="{Binding SelectToolCommand}"
                      CommandParameter="{x:Static viewmodels:ViewerTool.Pan}"
                      IsChecked="{Binding CurrentTool,
                                 Converter={StaticResource EnumToBoolConverter},
                                 ConverterParameter={x:Static viewmodels:ViewerTool.Pan}}" />

        <!-- Zoom 도구 선택 -->
        <ToggleButton Content="Zoom"
                      Command="{Binding SelectToolCommand}"
                      CommandParameter="{x:Static viewmodels:ViewerTool.Zoom}"
                      IsChecked="{Binding CurrentTool,
                                 Converter={StaticResource EnumToBoolConverter},
                                 ConverterParameter={x:Static viewmodels:ViewerTool.Zoom}}" />

        <!-- Window/Level 도구 선택 -->
        <ToggleButton Content="W/L"
                      Command="{Binding SelectToolCommand}"
                      CommandParameter="{x:Static viewmodels:ViewerTool.WindowLevel}"
                      IsChecked="{Binding CurrentTool,
                                 Converter={StaticResource EnumToBoolConverter},
                                 ConverterParameter={x:Static viewmodels:ViewerTool.WindowLevel}}" />
    </StackPanel>

</UserControl>
```

---

## 주의사항

1. **네임스페이스 선언 필수**: enum이 정의된 어셈블리와 네임스페이스를 XAML에 선언해야 함
2. **어셈블리 참조**: 다른 프로젝트의 enum을 사용할 경우 `assembly=` 지정 필요
3. **Converter에도 적용**: `ConverterParameter`에도 동일하게 `x:Static` 사용

---

## 관련 패턴

### EnumToBoolConverter (선택 상태 확인용)
```csharp
public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.Equals(parameter) ?? false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? parameter : Binding.DoNothing;
    }
}
```
