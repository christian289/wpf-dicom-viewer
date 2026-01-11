---
name: wpf-icon-font-inheritance
description: 'WPF CustomControl에서 Segoe Fluent Icons 사용 시 텍스트 폰트 상속 문제 해결'
---

# WPF CustomControl Icon Font 상속 문제 해결

## 문제 상황

WPF CustomControl에서 Segoe Fluent Icons 폰트를 사용할 때, 같은 ControlTemplate 내의 **TextBlock이 아이콘 폰트를 상속받아 텍스트가 깨지는 문제**가 발생합니다.

### 증상
- 버튼의 텍스트가 네모 박스(□)나 이상한 기호로 표시됨
- 아이콘은 정상적으로 표시되지만 일반 텍스트가 렌더링되지 않음

### 원인
WPF의 `FontFamily`는 Visual Tree를 따라 자식 요소에 상속됩니다. ControlTemplate 내에서 아이콘용 TextBlock에 `FontFamily="Segoe Fluent Icons"`를 설정하면, 같은 컨테이너 내의 다른 TextBlock도 이 폰트를 상속받을 수 있습니다.

---

## 해결 방법

### 텍스트 표시용 요소에 명시적 FontFamily 지정

```xml
<!-- IconButton ControlTemplate 예시 -->
<ControlTemplate TargetType="{x:Type local:IconButton}">
    <Border Background="{TemplateBinding Background}"
            BorderBrush="{TemplateBinding BorderBrush}"
            BorderThickness="{TemplateBinding BorderThickness}">
        <StackPanel Orientation="{TemplateBinding Orientation}"
                    HorizontalAlignment="Center"
                    VerticalAlignment="Center">

            <!-- 아이콘: Segoe Fluent Icons 사용 -->
            <TextBlock x:Name="PART_Icon"
                       Text="{TemplateBinding Icon}"
                       FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                       FontSize="{TemplateBinding IconSize}"
                       Foreground="{TemplateBinding Foreground}" />

            <!-- 텍스트: 명시적으로 Segoe UI 지정 (중요!) -->
            <TextBlock x:Name="PART_Text"
                       Text="{TemplateBinding Text}"
                       FontFamily="Segoe UI"
                       FontSize="12"
                       Foreground="{TemplateBinding Foreground}"
                       VerticalAlignment="Center" />
        </StackPanel>
    </Border>
</ControlTemplate>
```

---

## 핵심 포인트

1. **아이콘 폰트는 해당 요소에만 적용**: `PART_Icon`에만 `Segoe Fluent Icons` 지정
2. **텍스트 요소에 명시적 FontFamily 지정**: `PART_Text`에 `FontFamily="Segoe UI"` 필수
3. **상위 컨테이너에 FontFamily 지정 금지**: Border나 StackPanel에 FontFamily를 지정하면 모든 자식에 상속됨

---

## 적용 대상

- IconButton (아이콘 + 텍스트 버튼)
- IconToggleButton (토글 가능한 아이콘 버튼)
- NavigationButton (네비게이션 메뉴 아이템)
- 기타 아이콘과 텍스트를 함께 사용하는 모든 CustomControl

---

## 관련 아이콘 폰트

| 폰트 이름 | Windows 버전 | 용도 |
|-----------|-------------|------|
| `Segoe Fluent Icons` | Windows 11+ | 최신 Fluent Design 아이콘 |
| `Segoe MDL2 Assets` | Windows 10+ | 기본 시스템 아이콘 |

### Fallback 패턴
```xml
FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
```
Windows 10에서도 호환되도록 fallback 폰트 지정
