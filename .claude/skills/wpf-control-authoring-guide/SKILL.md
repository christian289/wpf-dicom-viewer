---
name: wpf-control-authoring-guide
description: WPF 컨트롤 제작 결정 가이드. Control 생성 여부 판단, UserControl vs Control vs FrameworkElement 선택 기준, DependencyProperty/RoutedEvent 구현 패턴 제시. 새 컨트롤 생성 전 대안(Style, Template, Trigger) 검토 포함.
---

# WPF Control Authoring Guide

WPF 컨트롤 제작 시 의사결정을 위한 가이드.

## 1. 새 컨트롤이 필요한가?

**먼저 대안을 검토하라.** WPF의 확장성 덕분에 새 컨트롤 없이도 대부분 해결 가능하다.

| 요구사항              | 대안            | 예시                                 |
| --------------------- | --------------- | ------------------------------------ |
| 외관만 변경           | Style           | TextBlock을 빨간색 Arial 14pt로 통일 |
| 컨트롤 구조 변경      | ControlTemplate | RadioButton을 신호등 모양으로        |
| 데이터 표시 방식 변경 | DataTemplate    | ListBox 항목에 체크박스 추가         |
| 상태별 동작 변경      | Trigger         | 선택된 항목을 굵은 빨간색으로        |
| 복합 콘텐츠 표시      | Rich Content    | Button에 이미지+텍스트 함께 표시     |

**새 컨트롤이 필요한 경우:**

- 기존 컨트롤에 없는 새로운 기능/동작
- 재사용 가능한 복합 컴포넌트
- 특수한 입력/상호작용 패턴

---

## 2. 베이스 클래스 선택

```
┌─────────────────────────────────────────────────────────────┐
│                    컨트롤 유형 결정                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────────┐  │
│  │ UserControl │    │   Control   │    │ FrameworkElement│  │
│  └──────┬──────┘    └──────┬──────┘    └────────┬────────┘  │
│         │                  │                    │           │
│  기존 컨트롤 조합     ControlTemplate     직접 렌더링        │
│  빠른 개발           커스터마이징 지원    완전한 제어         │
│  템플릿 불가         테마 지원            성능 최적화         │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### UserControl 선택 조건

- ✅ 기존 컨트롤 조합으로 충분
- ✅ 애플리케이션과 유사한 개발 방식 선호
- ✅ ControlTemplate 커스터마이징 불필요
- ❌ 테마 지원 불필요

### Control 선택 조건 (권장)

- ✅ ControlTemplate으로 외관 커스터마이징 필요
- ✅ 다양한 테마 지원 필요
- ✅ WPF 기본 컨트롤 수준의 확장성 필요
- ✅ UI와 로직의 완전한 분리

### FrameworkElement 선택 조건

- ✅ 단순 요소 조합으로 불가능한 외관
- ✅ OnRender로 직접 렌더링 필요
- ✅ DrawingVisual 기반 커스텀 조합
- ✅ 극한의 성능 최적화 필요

---

## 3. 스타일 가능한 컨트롤 설계 원칙

### 3.1 Template Contract를 엄격히 강제하지 마라

```csharp
// ❌ 잘못된 예: Part가 없으면 예외
public override void OnApplyTemplate()
{
    var button = GetTemplateChild("PART_Button") as Button;
    if (button == null)
        throw new InvalidOperationException("PART_Button required!");
}

// ✅ 올바른 예: Part가 없어도 동작
public override void OnApplyTemplate()
{
    base.OnApplyTemplate();
    ButtonElement = GetTemplateChild("PART_Button") as Button;
    // null이면 해당 기능만 비활성화, 컨트롤은 계속 동작
}
```

**핵심 원칙:**

- 디자인 타임에 ControlTemplate이 불완전할 수 있음
- Panel은 자식이 너무 많거나 적어도 예외 발생시키지 않음
- 필수 요소가 없으면 해당 기능만 비활성화

### 3.2 Helper Element 패턴

| 유형           | 설명                              | 예시                             |
| -------------- | --------------------------------- | -------------------------------- |
| **Standalone** | 독립적, 재사용 가능               | Popup, ScrollViewer, TabPanel    |
| **Type-based** | TemplatedParent 인식, 자동 바인딩 | ContentPresenter, ItemsPresenter |
| **Named**      | x:Name으로 코드에서 참조          | PART_TextBox, PART_Button        |

```csharp
// Type-based: ContentPresenter는 자동으로 TemplatedParent.Content에 바인딩
<ContentPresenter />

// Named: 코드에서 직접 참조 필요
<TextBox x:Name="PART_EditableTextBox" />
```

### 3.3 상태/동작 표현 우선순위

상위 항목일수록 우선 사용:

1. **Property Binding** - `ComboBox.IsDropDownOpen` ↔ `ToggleButton.IsChecked`
2. **Trigger/Animation** - Hover 상태에서 배경색 변경
3. **Command** - `ScrollBar.LineUpCommand`
4. **Standalone Helper** - `TabPanel` in `TabControl`
5. **Type-based Helper** - `ContentPresenter` in `Button`
6. **Named Helper** - `TextBox` in `ComboBox`
7. **Bubbled Event** - Named 요소에서 버블링되는 이벤트
8. **Custom OnRender** - `ButtonChrome` in `Button`

---

## 4. DependencyProperty 구현

스타일, 바인딩, 애니메이션, 동적 리소스를 지원하려면 DependencyProperty 필수.

```csharp
public static readonly DependencyProperty ValueProperty =
    DependencyProperty.Register(
        nameof(Value),
        typeof(int),
        typeof(NumericUpDown),
        new FrameworkPropertyMetadata(
            defaultValue: 0,
            propertyChangedCallback: OnValueChanged,
            coerceValueCallback: CoerceValue));

public int Value
{
    get => (int)GetValue(ValueProperty);
    set => SetValue(ValueProperty, value);
}

// ⚠️ CLR 래퍼에 로직 추가 금지! 바인딩 시 우회됨
// 대신 콜백 사용:
private static void OnValueChanged(DependencyObject d,
    DependencyPropertyChangedEventArgs e) { }

private static object CoerceValue(DependencyObject d, object value)
    => Math.Clamp((int)value, 0, 100);
```

---

## 5. RoutedEvent 구현

버블링, EventSetter, EventTrigger 지원을 위해 RoutedEvent 사용.

```csharp
public static readonly RoutedEvent ValueChangedEvent =
    EventManager.RegisterRoutedEvent(
        nameof(ValueChanged),
        RoutingStrategy.Bubble,
        typeof(RoutedPropertyChangedEventHandler<int>),
        typeof(NumericUpDown));

public event RoutedPropertyChangedEventHandler<int> ValueChanged
{
    add => AddHandler(ValueChangedEvent, value);
    remove => RemoveHandler(ValueChangedEvent, value);
}

protected virtual void OnValueChanged(RoutedPropertyChangedEventArgs<int> e)
    => RaiseEvent(e);
```

---

## 6. 커스터마이징 지원 전략

```
┌────────────────────────────────────────────────────────────┐
│              커스터마이징 빈도별 노출 전략                    │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  매우 빈번  →  DependencyProperty로 노출                   │
│              (Background, Foreground 등)                   │
│                                                            │
│  가끔       →  Attached Property로 노출                    │
│              (Grid.Row, Canvas.Left 등)                    │
│                                                            │
│  드물게    →  ControlTemplate 재정의 유도                   │
│              (문서화 필수)                                  │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

---

## 7. 테마 리소스 구성

```
📁 Themes/
├── Generic.xaml          ← 기본 (필수)
├── Aero.NormalColor.xaml ← Windows Vista/7
├── Luna.NormalColor.xaml ← Windows XP Blue
├── Luna.Homestead.xaml   ← Windows XP Olive
└── Luna.Metallic.xaml    ← Windows XP Silver
```

**AssemblyInfo.cs에 ThemeInfo 추가:**

```csharp
[assembly: ThemeInfo(
    ResourceDictionaryLocation.SourceAssembly,  // 테마별 리소스
    ResourceDictionaryLocation.SourceAssembly)] // Generic 리소스
```

**정적 생성자에서 DefaultStyleKey 설정:**

```csharp
static NumericUpDown()
{
    DefaultStyleKeyProperty.OverrideMetadata(
        typeof(NumericUpDown),
        new FrameworkPropertyMetadata(typeof(NumericUpDown)));
}
```

---

## 의사결정 체크리스트

### 새 컨트롤 생성 전

- [ ] Style로 해결 가능한가?
- [ ] ControlTemplate으로 해결 가능한가?
- [ ] DataTemplate으로 해결 가능한가?
- [ ] Trigger로 해결 가능한가?
- [ ] Rich Content로 해결 가능한가?

### 베이스 클래스 선택

- [ ] ControlTemplate 커스터마이징 필요? → Control
- [ ] 테마 지원 필요? → Control
- [ ] 기존 컨트롤 조합만으로 충분? → UserControl
- [ ] 직접 렌더링 필요? → FrameworkElement

### 컨트롤 설계

- [ ] Template Contract를 최소화했는가?
- [ ] Part 누락 시에도 동작하는가?
- [ ] 예외 대신 기능 비활성화로 처리하는가?
- [ ] 상태 표현에 우선순위를 따랐는가?

### 속성/이벤트

- [ ] 스타일/바인딩 지원 속성은 DependencyProperty인가?
- [ ] CLR 래퍼에 로직이 없는가?
- [ ] 이벤트는 RoutedEvent로 구현했는가?

### 테마/리소스

- [ ] Generic.xaml에 기본 스타일이 있는가?
- [ ] ThemeInfo 특성을 설정했는가?
- [ ] DefaultStyleKey를 설정했는가?
