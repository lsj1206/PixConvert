using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PixConvert.Controls;

public class OptionToolTip : StackPanel
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(OptionToolTip), new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty BodyProperty =
        DependencyProperty.Register(nameof(Body), typeof(string), typeof(OptionToolTip), new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty CautionProperty =
        DependencyProperty.Register(nameof(Caution), typeof(string), typeof(OptionToolTip), new PropertyMetadata(string.Empty, OnTextChanged));

    private readonly TextBlock _titleText;
    private readonly TextBlock _bodyText;
    private readonly TextBlock _cautionText;

    public OptionToolTip()
    {
        MaxWidth = 360;
        Orientation = Orientation.Vertical;

        _titleText = CreateTextBlock(fontSize: 12);
        _bodyText = CreateTextBlock(fontSize: 12);
        _cautionText = CreateTextBlock(fontSize: 11);

        _bodyText.Margin = new Thickness(0, 2, 0, 0);
        _cautionText.Margin = new Thickness(0, 2, 0, 0);
        _cautionText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
        _cautionText.FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI");

        Children.Add(_titleText);
        Children.Add(_bodyText);
        Children.Add(_cautionText);

        UpdateTextBlocks();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Body
    {
        get => (string)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public string Caution
    {
        get => (string)GetValue(CautionProperty);
        set => SetValue(CautionProperty, value);
    }

    private static TextBlock CreateTextBlock(double fontSize, FontWeight? fontWeight = null)
    {
        return new TextBlock
        {
            FontSize = fontSize,
            FontWeight = fontWeight ?? FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None
        };
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OptionToolTip toolTip)
            toolTip.UpdateTextBlocks();
    }

    private void UpdateTextBlocks()
    {
        UpdateTextBlock(_titleText, Title);
        UpdateTextBlock(_bodyText, Body);
        UpdateTextBlock(_cautionText, Caution);
    }

    private static void UpdateTextBlock(TextBlock textBlock, string? text)
    {
        textBlock.Text = text ?? string.Empty;
        textBlock.Visibility = string.IsNullOrWhiteSpace(text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
