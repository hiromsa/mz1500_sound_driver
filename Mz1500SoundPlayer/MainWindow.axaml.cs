using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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

        // テキストエリア等でイベントが消費される前にCaptureするため、Tunnel戦略でWindow全体にフックする
        this.AddHandler(InputElement.KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);
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

    private async void LoadMidiButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Open MIDI File",
                        AllowMultiple = false,
                        FileTypeFilter = new[] { new FilePickerFileType("MIDI Files") { Patterns = new[] { "*.mid", "*.midi" } } }
                    });

                    if (files.Count >= 1)
                    {
                        string filePath = files[0].Path.LocalPath;
                        var converter = new MidiToMmlConverter();
                        string mml = converter.Convert(filePath);
                        MmlInput.Text = mml;
                        LogOutput.Text = $"Loaded {files[0].Name} successfully.";
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogOutput.Text = $"MIDI Load Error: {ex.Message}";
            }
            finally
            {
                btn.IsEnabled = true;
            }
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

    private void Window_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
        {
            e.Handled = true;
            if (PlayMmlButton.IsEnabled)
            {
                // 再生開始
                PlayMmlButton_Click(PlayMmlButton, new RoutedEventArgs());
            }
            else
            {
                // 再生中は停止
                StopButton_Click(StopButton, new RoutedEventArgs());
            }
        }
    }

    private void MasterVolumeSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_player != null)
        {
            _player.MasterVolume = (float)e.NewValue;
        }
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

    private bool _isUpdatingCheckboxes = false;
    private void ChkAll_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingCheckboxes) return;
        _isUpdatingCheckboxes = true;
        bool isChecked = ChkAll.IsChecked ?? false;
        ChkA.IsChecked = isChecked;
        ChkB.IsChecked = isChecked;
        ChkC.IsChecked = isChecked;
        ChkD.IsChecked = isChecked;
        ChkE.IsChecked = isChecked;
        ChkF.IsChecked = isChecked;
        ChkG.IsChecked = isChecked;
        ChkH.IsChecked = isChecked;
        ChkP.IsChecked = isChecked;
        _isUpdatingCheckboxes = false;
        UpdateChannelMask();
    }

    private void ChkChannel_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingCheckboxes) return;
        _isUpdatingCheckboxes = true;
        
        bool allChecked = (ChkA.IsChecked == true) && (ChkB.IsChecked == true) && (ChkC.IsChecked == true) && (ChkD.IsChecked == true) && (ChkE.IsChecked == true) && (ChkF.IsChecked == true) && (ChkG.IsChecked == true) && (ChkH.IsChecked == true) && (ChkP.IsChecked == true);
        ChkAll.IsChecked = allChecked;
        
        _isUpdatingCheckboxes = false;
        UpdateChannelMask();
    }

    private void UpdateChannelMask()
    {
        if (_player == null) return;
        var activeChannels = new System.Collections.Generic.HashSet<string>();
        if (ChkA.IsChecked == true) activeChannels.Add("A");
        if (ChkB.IsChecked == true) activeChannels.Add("B");
        if (ChkC.IsChecked == true) activeChannels.Add("C");
        if (ChkD.IsChecked == true) activeChannels.Add("D");
        if (ChkE.IsChecked == true) activeChannels.Add("E");
        if (ChkF.IsChecked == true) activeChannels.Add("F");
        if (ChkG.IsChecked == true) activeChannels.Add("G");
        if (ChkH.IsChecked == true) activeChannels.Add("H");
        if (ChkP.IsChecked == true) activeChannels.Add("P");
        
        _player.ActiveChannels = activeChannels;
    }
}