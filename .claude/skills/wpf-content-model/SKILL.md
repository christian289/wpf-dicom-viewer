---
name: wpf-content-model
description: WPF 콘텐츠 모델 계층 구조. ContentControl, ItemsControl, HeaderedContentControl, HeaderedItemsControl 상속 패턴과 Content/Items 속성 활용. 컨트롤 선택 및 커스텀 컨트롤 설계 시 이 스킬 적용.
---

# WPF Content Model 패턴

WPF 컨트롤은 콘텐츠를 담는 방식에 따라 4가지 주요 모델로 분류됩니다.

## 1. 콘텐츠 모델 계층 구조

```
Control
├── ContentControl          (단일 Content)
│   ├── Button
│   ├── Label
│   ├── CheckBox
│   ├── RadioButton
│   ├── ToolTip
│   ├── ScrollViewer
│   ├── UserControl
│   ├── Window
│   └── HeaderedContentControl  (Content + Header)
│       ├── Expander
│       ├── GroupBox
│       └── TabItem
│
└── ItemsControl            (복수 Items)
    ├── ListBox
    ├── ComboBox
    ├── ListView
    ├── TreeView
    ├── Menu
    ├── TabControl
    └── HeaderedItemsControl    (Items + Header)
        ├── MenuItem
        ├── TreeViewItem
        └── ToolBar
```

---

## 2. ContentControl

### 2.1 특징

- **단일 Content 속성**: 하나의 자식 요소만 보유
- **Content 타입**: object (모든 타입 허용)
- **ContentTemplate**: Content 렌더링 방식 지정

### 2.2 기본 사용

```xml
<!-- 문자열 콘텐츠 -->
<!-- String content -->
<Button Content="Click Me"/>

<!-- 복합 콘텐츠 -->
<!-- Complex content -->
<Button>
    <StackPanel Orientation="Horizontal">
        <Image Source="/icon.png" Width="16"/>
        <TextBlock Text="Save" Margin="5,0,0,0"/>
    </StackPanel>
</Button>
```

### 2.3 ContentTemplate 활용

```xml
<Button Content="Download">
    <Button.ContentTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <Path Data="M12,2L12,14L8,10L12,14L16,10L12,14" 
                      Fill="White" Width="16"/>
                <TextBlock Text="{Binding}" Margin="5,0,0,0"/>
            </StackPanel>
        </DataTemplate>
    </Button.ContentTemplate>
</Button>
```

### 2.4 ContentControl 상속 컨트롤 생성

```csharp
namespace MyApp.Controls;

using System.Windows;
using System.Windows.Controls;

/// <summary>
/// 단일 콘텐츠를 표시하는 카드 컨트롤
/// Card control that displays single content
/// </summary>
public sealed class CardControl : ContentControl
{
    static CardControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(CardControl),
            new FrameworkPropertyMetadata(typeof(CardControl)));
    }

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(CardControl),
            new PropertyMetadata(new CornerRadius(8)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }
}
```

---

## 3. ItemsControl

### 3.1 특징

- **Items 컬렉션**: 복수의 자식 요소 보유
- **ItemsSource**: 데이터 바인딩용 소스
- **ItemTemplate**: 각 항목의 렌더링 방식
- **ItemsPanel**: 항목 배치 패널 지정

### 3.2 기본 사용

```xml
<!-- 직접 항목 추가 -->
<!-- Direct item addition -->
<ListBox>
    <ListBoxItem Content="Item 1"/>
    <ListBoxItem Content="Item 2"/>
    <ListBoxItem Content="Item 3"/>
</ListBox>

<!-- 데이터 바인딩 -->
<!-- Data binding -->
<ListBox ItemsSource="{Binding Products}">
    <ListBox.ItemTemplate>
        <DataTemplate>
            <StackPanel>
                <TextBlock Text="{Binding Name}" FontWeight="Bold"/>
                <TextBlock Text="{Binding Price, StringFormat=₩{0:N0}}"/>
            </StackPanel>
        </DataTemplate>
    </ListBox.ItemTemplate>
</ListBox>
```

### 3.3 ItemsPanel 커스터마이징

```xml
<!-- 가로 배치 리스트 -->
<!-- Horizontal list layout -->
<ListBox ItemsSource="{Binding Items}">
    <ListBox.ItemsPanel>
        <ItemsPanelTemplate>
            <StackPanel Orientation="Horizontal"/>
        </ItemsPanelTemplate>
    </ListBox.ItemsPanel>
</ListBox>

<!-- 그리드 배치 -->
<!-- Grid layout -->
<ItemsControl ItemsSource="{Binding Items}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <WrapPanel/>
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
</ItemsControl>

<!-- 가상화 패널 (대용량 데이터) -->
<!-- Virtualized panel (large data) -->
<ListBox ItemsSource="{Binding LargeCollection}"
         VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling">
    <ListBox.ItemsPanel>
        <ItemsPanelTemplate>
            <VirtualizingStackPanel/>
        </ItemsPanelTemplate>
    </ListBox.ItemsPanel>
</ListBox>
```

### 3.4 ItemsControl 상속 컨트롤 생성

```csharp
namespace MyApp.Controls;

using System.Windows;
using System.Windows.Controls;

/// <summary>
/// 태그 목록을 표시하는 컨트롤
/// Control that displays a list of tags
/// </summary>
public sealed class TagListControl : ItemsControl
{
    static TagListControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TagListControl),
            new FrameworkPropertyMetadata(typeof(TagListControl)));
    }

    protected override DependencyObject GetContainerForItemOverride()
    {
        // 각 항목을 감싸는 컨테이너 지정
        // Specify container that wraps each item
        return new TagItem();
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is TagItem;
    }
}

public sealed class TagItem : ContentControl
{
    static TagItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TagItem),
            new FrameworkPropertyMetadata(typeof(TagItem)));
    }
}
```

---

## 4. HeaderedContentControl

### 4.1 특징

- **Header + Content**: 두 영역 분리
- **HeaderTemplate**: Header 렌더링 방식
- **ContentTemplate**: Content 렌더링 방식

### 4.2 대표 컨트롤

```xml
<!-- GroupBox -->
<GroupBox Header="설정">
    <StackPanel>
        <CheckBox Content="옵션 1"/>
        <CheckBox Content="옵션 2"/>
    </StackPanel>
</GroupBox>

<!-- Expander -->
<Expander Header="상세 정보" IsExpanded="False">
    <TextBlock Text="접힌 상태에서는 보이지 않는 내용"/>
</Expander>

<!-- TabItem -->
<TabControl>
    <TabItem Header="탭 1">
        <TextBlock Text="탭 1 내용"/>
    </TabItem>
    <TabItem>
        <TabItem.Header>
            <StackPanel Orientation="Horizontal">
                <Ellipse Width="8" Height="8" Fill="Green"/>
                <TextBlock Text="상태" Margin="5,0,0,0"/>
            </StackPanel>
        </TabItem.Header>
        <TextBlock Text="탭 2 내용"/>
    </TabItem>
</TabControl>
```

### 4.3 HeaderedContentControl 상속 컨트롤 생성

```csharp
namespace MyApp.Controls;

using System.Windows;
using System.Windows.Controls;

/// <summary>
/// 접을 수 있는 섹션 컨트롤
/// Collapsible section control
/// </summary>
public sealed class CollapsibleSection : HeaderedContentControl
{
    static CollapsibleSection()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(CollapsibleSection),
            new FrameworkPropertyMetadata(typeof(CollapsibleSection)));
    }

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(
            nameof(IsExpanded),
            typeof(bool),
            typeof(CollapsibleSection),
            new PropertyMetadata(true));

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }
}
```

---

## 5. HeaderedItemsControl

### 5.1 특징

- **Header + Items**: 헤더와 복수 항목
- **계층적 구조** 표현에 적합

### 5.2 대표 컨트롤

```xml
<!-- TreeViewItem -->
<TreeView>
    <TreeViewItem Header="폴더 1">
        <TreeViewItem Header="파일 1.txt"/>
        <TreeViewItem Header="파일 2.txt"/>
        <TreeViewItem Header="하위 폴더">
            <TreeViewItem Header="파일 3.txt"/>
        </TreeViewItem>
    </TreeViewItem>
</TreeView>

<!-- MenuItem -->
<Menu>
    <MenuItem Header="파일">
        <MenuItem Header="새로 만들기"/>
        <MenuItem Header="열기"/>
        <Separator/>
        <MenuItem Header="최근 파일">
            <MenuItem Header="file1.txt"/>
            <MenuItem Header="file2.txt"/>
        </MenuItem>
    </MenuItem>
</Menu>
```

### 5.3 HierarchicalDataTemplate (계층 데이터 바인딩)

```xml
<TreeView ItemsSource="{Binding RootNodes}">
    <TreeView.ItemTemplate>
        <HierarchicalDataTemplate ItemsSource="{Binding Children}">
            <StackPanel Orientation="Horizontal">
                <Image Source="{Binding Icon}" Width="16"/>
                <TextBlock Text="{Binding Name}" Margin="5,0,0,0"/>
            </StackPanel>
        </HierarchicalDataTemplate>
    </TreeView.ItemTemplate>
</TreeView>
```

---

## 6. 컨트롤 선택 가이드

| 시나리오 | 추천 베이스 클래스 |
|---------|-------------------|
| 단일 콘텐츠 표시 | ContentControl |
| 단일 콘텐츠 + 제목 | HeaderedContentControl |
| 목록/컬렉션 표시 | ItemsControl |
| 선택 가능한 목록 | Selector (ListBox, ComboBox) |
| 계층적 데이터 | HeaderedItemsControl |
| 입력 필드 | TextBoxBase |
| 범위 값 선택 | RangeBase |

---

## 7. Content 속성 처리 과정

```
Content 설정
    ↓
ContentTemplate 있음?
    ├── Yes → DataTemplate으로 렌더링
    └── No → Content 타입 확인
                ├── UIElement → 직접 렌더링
                ├── String → TextBlock으로 렌더링
                └── 기타 → ToString() 후 TextBlock
```

---

## 8. 참고 문서

- [WPF Content Model - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/wpf-content-model)
- [ItemsControl Class - Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.itemscontrol)
