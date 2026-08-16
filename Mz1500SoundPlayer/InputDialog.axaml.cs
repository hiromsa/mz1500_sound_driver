using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace Mz1500SoundPlayer;

public partial class InputDialog : Window
{
    public string? Result { get; private set; }

    public InputDialog()
    {
        InitializeComponent();
    }

    public InputDialog(string title, string message, string defaultText = "") : this()
    {
        Title = title;
        MessageText.Text = message;
        InputTextBox.Text = defaultText;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        Result = InputTextBox.Text;
        Close(Result);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close(Result);
    }
}
