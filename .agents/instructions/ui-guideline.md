# XAML Coding Guideline (C# / MVVM / WPF / ModernWPF)

> **Scope**: New and modified files. (Apply only to changed parts in existing files.)

## Naming Conventions

| Target             | Rule                            | Example                         |
| ------------------ | ------------------------------- | ------------------------------- |
| `x:Name`           | PascalCase + Descriptive name   | `SubmitButton`, `SearchTextBox` |
| `Style` Key        | PascalCase + `Style` suffix     | `PrimaryButtonStyle`            |
| `DataTemplate` Key | PascalCase + `Template` suffix  | `FileItemTemplate`              |
| `Converter` Key    | PascalCase + `Converter` suffix | `BoolToVisibilityConverter`     |

- [MUST] Use `x:Name` only when necessary for Code-behind or `ElementName` bindings. Avoid unnecessary `x:Name` declarations.
- [AVOID] Vague abbreviations or numbered identifiers (e.g., `Btn1`, `TxtBox`, `Ctrl1`).

## Attribute Ordering

### 1st Order : Category (apply top-to-bottom)

| Priority | Category            | Attribute                                                                                                                                                                                                    |
| -------- | ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1        | Key / Name          | `x:Name`, `x:Key`                                                                                                                                                                                            |
| 2        | Attached Properties | `Grid.Row`, `Grid.Column`, `Grid.RowSpan`, `DockPanel.Dock`, `ui:ControlHelper.*`                                                                                                                            |
| 3        | Layout              | `Width`, `Height`, `MinWidth`, `MaxWidth`, `MinHeight`, `MaxHeight`, `Margin`, `Padding`, `HorizontalAlignment`, `VerticalAlignment`, `HorizontalContentAlignment`, `VerticalContentAlignment`, `Visibility` |
| 4        | Appearance          | `Background`, `Foreground`, `BorderBrush`, `BorderThickness`, `Opacity`, `Style`, `Template`                                                                                                                 |
| 5        | Content             | `Content`, `Header`, `Text`, `Icon`, `ToolTip`                                                                                                                                                               |
| 6        | Data                | `ItemsSource`, `DataContext`, `SelectedItem`, `{Binding ...}`                                                                                                                                                |
| 7        | Events / Commands   | `Command`, `CommandParameter`, `Click`, `SelectionChanged`, `i:Interaction.Triggers`                                                                                                                         |

### 2nd Order: keep logical pairs adjacent

```xml
Width / Height
MinWidth / Width / MaxWidth
MinHeight / Height / MaxHeight
HorizontalAlignment / VerticalAlignment
HorizontalContentAlignment / VerticalContentAlignment
```

- [MUST] For elements with 3 or more attributes, place one attribute per line.

## Formatting & Comments

- [MUST] Indentation: 4 spaces.
- [SHOULD] Break inline bindings exceeding 100 characters onto a new line.
- [MUST] Add hierarchical scope comments at the beginning of sections in large XAML files.

### Comment Format Example

```xml
<!-- [Scope] : [Function]_[Detail] -->
<!-- Sidebar : Sort_Dropdown -->
<!-- Sidebar : Sort_Reorder -->
```

## Binding

- [MUST] Use the standard syntax `{Binding Path=...}`.
- [SHOULD] Use `Mode=OneTime` for one-time display data to optimize performance.
- [SHOULD] Explicitly set `UpdateSourceTrigger=PropertyChanged` for two-way bindings that require real-time updates.
- [AVOID] `RelativeSource AncestorType`. Favor ViewModel bindings whenever possible, and use AncestorType only as a last resort.
- [AVOID] Complex inline `StringFormat` or `ConverterParameter` usage. Delegate such logic to the ViewModel or a dedicated Converter.

## ModernWPF & MVVM Rules

- [MUST] Declare `xmlns:ui="http://schemas.modernwpf.com/2019"` in the root element when using `ModernWPF`.
- [MUST] Always reference color and brush resources using `{DynamicResource ...}`.
- [MUST] For Event→Command binding, use `EventTrigger` from `Microsoft.Xaml.Behaviors.Wpf`
- [MUST] Keep business logic out of Code-behind. Delegate UI control logic to the ViewModel.
- [AVOID] Event handlers in Code-behind that directly mutate ViewModel state.

### xmlns declaration order (root element)

```xml
xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
xmlns:ui="http://schemas.modernwpf.com/2019"
xmlns:i="http://schemas.microsoft.com/xaml/behaviors"
xmlns:vm="clr-namespace:YourApp.ViewModels"
xmlns:conv="clr-namespace:YourApp.Converters"
```

## Style & Template Rules

- [MUST] Inline `Style` definitions are prohibited.
- [MUST] Declare `Styles` in a `ResourceDictionary` and reference them via `x:Key`.
- [SHOULD] Extract identical property combinations into a `Style` if repeated twice or more.
- [SHOULD] `DataTemplate` in `ItemsControl`'s `Resources` or dedicated `ResourceDictionary`.
- [AVOID] Custom `ControlTemplate` definitions. Use ModernWPF default template + property override if possible.

## Anti-patterns

| Anti-pattern                                   | Correct Pattern                                               |
| ---------------------------------------------- | ------------------------------------------------------------- |
| `Background="White"`                           | `Background="{DynamicResource SystemControlBackgroundBrush}"` |
| Referencing theme colors with `StaticResource` | Replace with `DynamicResource`                                |
| Assigning `x:Name` to all elements             | Use only for Code-behind/ElementName bindings                 |
| Handling events via `Click="Button_Click"`     | `Command="{Binding SomeCommand}"` + `EventTrigger`            |
| Listing 4+ attributes on a single line         | Place one attribute per line                                  |
| Inline `Style` definitions                     | Extract to a `ResourceDictionary` with an `x:Key`             |

## Examples

### Good Case

```xml
<!-- MainContent : FileList_Item -->
<Button x:Name="SubmitButton"
        Grid.Row="1" Grid.Column="2"
        ui:ControlHelper.CornerRadius="4"
        HorizontalAlignment="Right" VerticalAlignment="Center"
        Width="100" Height="32"
        Margin="8,0"
        Background="{DynamicResource SystemControlBackgroundAccentBrush}"
        Foreground="#FF000000"
        Content="완료"
        Command="{Binding SubmitCommand}" />
```

### Bad Case

```xml
<!-- Ignoring attribute order, Missing DynamicResource, Single-line attributes, Code-behind Event -->
<Button Content="완료" Width="100" Click="OnSubmit"
        Grid.Row="1" Background="White" HorizontalAlignment="Right"/>
```
