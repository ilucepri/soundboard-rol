using System.Windows;
using System.Windows.Input;

namespace Soundboard.Views;

public partial class TextPromptWindow : Window
{
    public TextPromptWindow(string title, string label, string initialValue)
    {
        InitializeComponent();
        Title = title;
        LabelText.Text = label;
        Input.Text = initialValue;
        Loaded += (_, _) =>
        {
            Input.Focus();
            Input.SelectAll();
        };
    }

    public string Value => Input.Text;

    void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Accept();
        else if (e.Key == Key.Escape) DialogResult = false;
    }

    void OnAccept(object sender, RoutedEventArgs e) => Accept();

    void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    void Accept() => DialogResult = true;
}
