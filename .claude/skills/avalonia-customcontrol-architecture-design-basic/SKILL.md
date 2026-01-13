---
name: avalonia-customcontrol-architecture-design-basic
description: 'AvaloniaUI CustomControl를 활용한 AvaloniaUI Desktop Application Solution 기본 구조'
---

# 6.5 Writing AXAML Code

- **When generating AXAML code, use CustomControl with ControlTheme for Stand-Alone Control Style**
- Purpose: Theme separation and minimizing style dependencies

#### 6.5.1 AvaloniaUI Custom Control Library Project Structure

**Recommended Project Structure:**

```
YourAvaloniaSolution
├── YourCustomControlProject1/
│    ├── Properties/
│    │   ├── AssemblyInfo.cs            ← AssemblyInfo.cs definition
│    ├── Themes/
│    │   ├── Generic.axaml            ← ControlTheme definition
│    │   ├── CustomButton1.axaml       ← Individual control theme
│    │   └── CustomTextBox1.axaml      ← Individual control theme
│    ├── CustomButton1.cs
│    └── CustomTextBox1.cs
└── YourCustomControlProject2/
    ├── Properties/
    │   ├── AssemblyInfo.cs            ← AssemblyInfo.cs definition
    ├── Themes/
    │   ├── Generic.axaml            ← ControlTheme definition
    │   ├── CustomButton2.axaml       ← Individual control theme
    │   └── CustomTextBox2.axaml      ← Individual control theme
    ├── CustomButton2.cs
    └── CustomTextBox2.cs
```

# 6.6 ⚠️ Distinguishing ResourceInclude vs MergeResourceInclude

- **ResourceInclude**: Used in regular ResourceDictionary files (Generic.axaml, Styles, etc.)
- **MergeResourceInclude**: Used only in Application.Resources (App.axaml)

**Advantages:**

- Complete separation of theme and logic based on ControlTheme
- Flexible style variations through CSS Classes
- State management via Pseudo Classes (:pointerover, :pressed, etc.)
- Theme modularization through ResourceInclude
- Work can be split by file for team collaboration

#### 6.5.2 Key Differences Between WPF and AvaloniaUI

| Item              | WPF                                     | AvaloniaUI                           |
| ----------------- | --------------------------------------- | ------------------------------------ |
| File Extension    | .xaml                                   | .axaml                               |
| Style Definition  | Style + ControlTemplate                 | ControlTheme                         |
| State Management  | Trigger, DataTrigger                    | Pseudo Classes, Style Selector       |
| CSS Support       | ❌                                      | ✅ (Classes attribute)               |
| Resource Merging  | MergedDictionaries + ResourceDictionary | MergedDictionaries + ResourceInclude |
| Dependency Props  | DependencyProperty                      | StyledProperty, DirectProperty       |

