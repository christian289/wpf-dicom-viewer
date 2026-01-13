---
name: wpf-customcontrol-development
description: WPF CustomControl 개발을 위한 Parts and States Model 기반 Best Practice. ControlTemplate을 통해 외관 커스터마이징이 가능한 컨트롤 개발 시 사용. (1) Control 클래스 상속 컨트롤 생성, (2) TemplatePart/TemplateVisualState 특성 정의, (3) OnApplyTemplate 구현, (4) VisualStateManager 상태 관리 시 이 스킬 적용.
---

# WPF CustomControl Development - Parts and States Model

외관 커스터마이징이 가능한 WPF CustomControl 개발 워크플로우.

## 개발 Flow

### 1. Control 상속 결정

```
UserControl 선택 조건:
- 빠른 개발 필요
- ControlTemplate 커스터마이징 불필요
- 복잡한 테마 지원 불필요

Control 상속 선택 조건:
- ControlTemplate으로 외관 커스터마이징 필요
- 다양한 테마 지원 필요
- WPF 기본 컨트롤과 동일한 확장성 필요
```

### 2. Control Contract 정의

클래스에 `TemplatePart`와 `TemplateVisualState` 특성 선언:

```csharp
[TemplatePart(Name = PartUpButton, Type = typeof(RepeatButton))]
[TemplatePart(Name = PartDownButton, Type = typeof(RepeatButton))]
[TemplateVisualState(Name = StatePositive, GroupName = GroupValueStates)]
[TemplateVisualState(Name = StateNegative, GroupName = GroupValueStates)]
[TemplateVisualState(Name = StateFocused, GroupName = GroupFocusStates)]
[TemplateVisualState(Name = StateUnfocused, GroupName = GroupFocusStates)]
public class NumericUpDown : Control
{
    // Part/State 이름은 const로 정의
    private const string PartUpButton = "PART_UpButton";
    private const string PartDownButton = "PART_DownButton";
    private const string GroupValueStates = "ValueStates";
    private const string GroupFocusStates = "FocusStates";
    private const string StatePositive = "Positive";
    private const string StateNegative = "Negative";
    private const string StateFocused = "Focused";
    private const string StateUnfocused = "Unfocused";
}
```

### 3. Template Part 속성 패턴

Part 요소는 private 속성으로 래핑하고, setter에서 이벤트 구독/해제:

```csharp
private RepeatButton? _upButton;
private RepeatButton? UpButtonElement
{
    get => _upButton;
    set
    {
        // 기존 요소의 이벤트 해제
        if (_upButton is not null)
            _upButton.Click -= OnUpButtonClick;

        _upButton = value;

        // 새 요소의 이벤트 구독
        if (_upButton is not null)
            _upButton.Click += OnUpButtonClick;
    }
}
```

### 4. OnApplyTemplate 구현

```csharp
public override void OnApplyTemplate()
{
    base.OnApplyTemplate();

    // GetTemplateChild + as 캐스팅 (타입 불일치 시 null)
    UpButtonElement = GetTemplateChild(PartUpButton) as RepeatButton;
    DownButtonElement = GetTemplateChild(PartDownButton) as RepeatButton;

    // 초기 상태 설정 (transition 없이)
    UpdateStates(useTransitions: false);
}
```

**핵심 원칙:**

- Part가 없거나 타입이 다르면 null → 에러 발생시키지 않음
- 컨트롤은 불완전한 ControlTemplate에서도 동작해야 함

### 5. UpdateStates 헬퍼 메서드

상태 전환 로직을 단일 메서드로 중앙화:

```csharp
private void UpdateStates(bool useTransitions)
{
    // ValueStates 그룹
    VisualStateManager.GoToState(this,
        Value >= 0 ? StatePositive : StateNegative,
        useTransitions);

    // FocusStates 그룹
    VisualStateManager.GoToState(this,
        IsFocused ? StateFocused : StateUnfocused,
        useTransitions);
}
```

**UpdateStates 호출 시점:**

- `OnApplyTemplate` - 초기 상태 (useTransitions: false)
- 속성 변경 콜백 - 값 변경 반영 (useTransitions: true)
- `OnGotFocus`/`OnLostFocus` - 포커스 상태 (useTransitions: true)

### 6. 속성 변경 콜백

```csharp
private static void OnValueChanged(DependencyObject d,
    DependencyPropertyChangedEventArgs e)
{
    var control = (NumericUpDown)d;
    control.UpdateStates(useTransitions: true);
    control.OnValueChanged(new ValueChangedEventArgs((int)e.NewValue));
}
```

### 7. ControlTemplate 구조 (Generic.xaml)

```xml
<Style TargetType="{x:Type local:NumericUpDown}">
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="{x:Type local:NumericUpDown}">
        <Grid Background="{TemplateBinding Background}">

          <!-- VisualStateGroups는 루트 요소에 배치 -->
          <VisualStateManager.VisualStateGroups>
            <VisualStateGroup x:Name="ValueStates">
              <VisualState x:Name="Positive"/>
              <VisualState x:Name="Negative">
                <Storyboard>
                  <ColorAnimation To="Red"
                    Storyboard.TargetName="ValueText"
                    Storyboard.TargetProperty="(Foreground).(SolidColorBrush.Color)"/>
                </Storyboard>
              </VisualState>
            </VisualStateGroup>

            <VisualStateGroup x:Name="FocusStates">
              <VisualState x:Name="Focused">
                <Storyboard>
                  <ObjectAnimationUsingKeyFrames
                    Storyboard.TargetName="FocusVisual"
                    Storyboard.TargetProperty="Visibility">
                    <DiscreteObjectKeyFrame KeyTime="0" Value="{x:Static Visibility.Visible}"/>
                  </ObjectAnimationUsingKeyFrames>
                </Storyboard>
              </VisualState>
              <VisualState x:Name="Unfocused"/>
            </VisualStateGroup>
          </VisualStateManager.VisualStateGroups>

          <!-- Part 요소는 x:Name으로 정의 -->
          <RepeatButton x:Name="PART_UpButton" Content="▲"/>
          <TextBlock x:Name="ValueText" Text="{TemplateBinding Value}"/>
          <RepeatButton x:Name="PART_DownButton" Content="▼"/>

        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
```

## 체크리스트

- [ ] Control 클래스 상속 (UserControl 아님)
- [ ] `TemplatePart` 특성으로 필수 Part 선언
- [ ] `TemplateVisualState` 특성으로 상태 선언
- [ ] Part/State 이름은 const 문자열로 정의
- [ ] Part 속성 setter에서 이벤트 구독/해제
- [ ] `OnApplyTemplate`에서 `GetTemplateChild` + null 허용
- [ ] `UpdateStates` 헬퍼로 상태 전환 중앙화
- [ ] ControlTemplate 루트에 `VisualStateManager.VisualStateGroups` 배치
- [ ] Themes/Generic.xaml에 기본 스타일 배치
- [ ] 정적 생성자에서 `DefaultStyleKeyProperty.OverrideMetadata` 호출
