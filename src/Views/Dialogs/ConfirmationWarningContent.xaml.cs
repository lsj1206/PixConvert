using System.Windows;
using System.Windows.Controls;

namespace PixConvert.Views.Dialogs;

public partial class ConfirmationWarningContent : UserControl
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message),
            typeof(string),
            typeof(ConfirmationWarningContent),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty WarningMessageProperty =
        DependencyProperty.Register(
            nameof(WarningMessage),
            typeof(string),
            typeof(ConfirmationWarningContent),
            new PropertyMetadata(string.Empty));

    public ConfirmationWarningContent()
    {
        InitializeComponent();
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string WarningMessage
    {
        get => (string)GetValue(WarningMessageProperty);
        set => SetValue(WarningMessageProperty, value);
    }
}
