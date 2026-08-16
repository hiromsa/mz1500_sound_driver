using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Mz1500SoundPlayer;

public partial class ConfirmDialog : Window
{
    public bool Result { get; private set; }

    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(string title, string message) : this()
    {
        Title = title;
        MessageText.Text = message;
    }

    private void Yes_Click(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close(Result);
    }

    private void No_Click(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close(Result);
    }
}