using System.Windows;
using System.Windows.Input;

namespace Soundboard.Views;

public partial class ProfileWindow : Window
{
    /// <summary>Los que ofrece el diseño. El modelo admite cualquier texto, así que no es una lista cerrada.</summary>
    static readonly string[] Icons = ["🎲", "🎻", "🪄", "🗡️", "🍺", "🐉", "🔥", "👻", "⚔️", "📯"];

    public ProfileWindow(string title, string name, string icon)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        NameBox.Text = name;

        IconList.ItemsSource = Icons;
        IconList.SelectedItem = Icons.Contains(icon) ? icon : Icons[0];

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public string ProfileName => NameBox.Text;

    public string ProfileIcon => IconList.SelectedItem as string ?? Icons[0];

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) DialogResult = true;
        else if (e.Key == Key.Escape) DialogResult = false;
    }

    void OnAccept(object sender, RoutedEventArgs e) => DialogResult = true;

    void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
