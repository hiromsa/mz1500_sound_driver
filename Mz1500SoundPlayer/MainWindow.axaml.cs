using Avalonia.Controls;
using Avalonia.Interactivity;
using Mz1500SoundPlayer.Sound;
using System.Threading.Tasks;

namespace Mz1500SoundPlayer;

public partial class MainWindow : Window
{
    private readonly MmlPlayerModel _player;

    public MainWindow()
    {
        InitializeComponent();
        _player = new MmlPlayerModel();
        
        // アプリ終了時に確実に音を止めるための処理
        this.Closed += (s, e) => _player.Stop();
    }

    private async void PlayDemoButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            await _player.PlaySequenceAsync();
            btn.IsEnabled = true;
        }
    }

    private void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        MmlInput.Text = "";
    }

    private async void PlayMmlButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            try
            {
                string mml = MmlInput.Text ?? "";
                string log = await _player.PlayMmlAsync(mml);
                LogOutput.Text = log;
            }
            catch (System.Exception ex)
            {
                // エラー時は何もしない簡易実装
                System.Console.WriteLine($"MML Parse Error: {ex.Message}");
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }
}