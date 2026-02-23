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
                LogOutput.Text = $"MML Parse Error: {ex.Message}";
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }

    private void StopButton_Click(object? sender, RoutedEventArgs e)
    {
        _player.Stop();
        LogOutput.Text = "Playback stopped.";
    }

    private void ExportQdcButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string mml = MmlInput.Text ?? "";
            
            // 本当はSaveFileDialogを使うべきだが、簡便化のため実行ファイルと同じ場所に固定で吐き出す
            string outPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "output.qdc");
            
            string log = _player.ExportQdc(mml, outPath);
            LogOutput.Text = log;
        }
        catch (System.Exception ex)
        {
            LogOutput.Text = $"Export Error: {ex.Message}";
        }
    }
}