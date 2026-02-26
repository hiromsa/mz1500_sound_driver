using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Mz1500SoundPlayer.Sound;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using System.Xml;
using System.Reflection;
using Avalonia.Threading;
using System.Linq;

namespace Mz1500SoundPlayer;

public partial class MainWindow : Window
{
    private readonly MmlPlayerModel _player;
    private readonly PlaybackHighlightRenderer _highlightRenderer;
    private readonly DispatcherTimer _playbackTimer;

    public MainWindow()
    {
        InitializeComponent();
        _player = new MmlPlayerModel();
        
        // Setup Highlight Renderer
        _highlightRenderer = new PlaybackHighlightRenderer();
        MmlInput.TextArea.TextView.BackgroundRenderers.Add(_highlightRenderer);

        // Setup Playback Timer for UI updates (~30fps)
        _playbackTimer = new DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(33)
        };
        _playbackTimer.Tick += PlaybackTimer_Tick;
        
        // アプリ終了時に確実に音を止めるための処理
        this.Closed += (s, e) => _player.Stop();

        // テキストエリア等でイベントが消費される前にCaptureするため、Tunnel戦略でWindow全体にフックする
        this.AddHandler(InputElement.KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);

        // Load custom MML syntax highlighting
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("Mz1500SoundPlayer.MmlSyntax.xshd"))
        {
            if (stream != null)
            {
                using (var reader = new XmlTextReader(stream))
                {
                    MmlInput.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
        }
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

    private async void LoadFmsButton_Click(object? sender, RoutedEventArgs e)
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
                        Title = "Open FamiStudio Project",
                        AllowMultiple = false,
                        FileTypeFilter = new[] { new FilePickerFileType("FamiStudio Files") { Patterns = new[] { "*.fms" } } }
                    });

                    if (files.Count >= 1)
                    {
                        string filePath = files[0].Path.LocalPath;
                        var fmsLoader = new FamiStudio.ProjectFile();
                        var project = fmsLoader.Load(filePath);
                        if (project != null)
                        {
                            string mml = FamiStudioToMmlConverter.Convert(project, 0);
                            MmlInput.Text = string.IsNullOrEmpty(mml) ? "; No MML output" : mml;
                            LogOutput.Text = $"Loaded {files[0].Name} successfully.";
                        }
                        else
                        {
                            LogOutput.Text = $"Failed to parse {files[0].Name}. Error loading .fms";
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogOutput.Text = $"FMS Load Error: {ex.Message}";
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }

    private async void LoadMmlButton_Click(object? sender, RoutedEventArgs e)
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
                        Title = "Open MML File",
                        AllowMultiple = false,
                        FileTypeFilter = new[] 
                        { 
                            new FilePickerFileType("MML Files") { Patterns = new[] { "*.mml" } },
                            new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                            new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                        }
                    });

                    if (files.Count >= 1)
                    {
                        string filePath = files[0].Path.LocalPath;
                        string mml = await File.ReadAllTextAsync(filePath);
                        MmlInput.Text = mml;
                        LogOutput.Text = $"Loaded MML: {files[0].Name}";
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogOutput.Text = $"MML Load Error: {ex.Message}";
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }

    private async void SaveMmlButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Save MML File",
                        DefaultExtension = ".mml",
                        FileTypeChoices = new[] 
                        { 
                            new FilePickerFileType("MML Files") { Patterns = new[] { "*.mml" } },
                            new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } }
                        }
                    });

                    if (file != null)
                    {
                        string filePath = file.Path.LocalPath;
                        await File.WriteAllTextAsync(filePath, MmlInput.Text ?? "");
                        LogOutput.Text = $"Saved MML to: {file.Name}";
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogOutput.Text = $"MML Save Error: {ex.Message}";
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }

    private async void RemapButton_Click(object? sender, RoutedEventArgs e)
    {
        string text = MmlInput.Text ?? "";
        
        // Find which channels actually have data
        var usedChannels = new HashSet<string>();
        var linesForScan = text.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        foreach (var line in linesForScan)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith(";") || line.TrimStart().StartsWith("/"))
                continue;

            for(int c=0; c<line.Length; c++)
            {
                char ch = line[c];
                if (char.IsWhiteSpace(ch)) continue;
                if ((ch >= 'A' && ch <= 'H') || ch == 'P' || (ch >= 'a' && ch <= 'h'))
                {
                    usedChannels.Add(char.ToUpper(ch).ToString());
                }
                else 
                {
                    break; // End of track header
                }
            }
        }

        var remapWindow = new ChannelRemapWindow(usedChannels);
        
        // Show dialog and wait for the returned Dictionary map
        var result = await remapWindow.ShowDialog<Dictionary<string, string>>(this);
        
        if (result != null && result.Count > 0)
        {
            try
            {
                
                // Step 1: Replace original target channel specifiers (A-H, P) with temporary tags
                // We use Regex to match ONLY track definitions at the start of a line or block
                // Specifically matching characters A-H, P that stand alone or are followed by spaces/other track names.
                // We avoid replacing notes (a-g).
                // MML syntax uses upper case for tracks.
                
                string tagPrefix = "_{TMP_REMAP_CH_";
                string tagSuffix = "}_";

                // To ensure safe replacement, we iterate character by character for the track headers,
                // or we use a multi-pass regex. A simple approach for the whole document is risky because
                // upper case A-H, P might appear elsewhere (like comments or EP commands).
                
                // Track commands appear at the beginning of a line, or after spaces.
                // It's safest to find lines that start with A-H, P (with possible spaces)
                // and just replace the characters inside the track header block.
                
                // Split by line
                var lines = text.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    
                    // Skip comments
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith(";") || line.TrimStart().StartsWith("/"))
                        continue;

                    // Match track prefix (e.g., "ABCDE ", "A ", "P t144")
                    var match = Regex.Match(line, @"^([A-Ha-hP\s]+)(?:[^\+\-\#\s]|$)");
                    
                    // But wait, the regex above is tricky. The simplest robust way is to just replace 
                    // isolated uppercase letters A-H, P at the very beginning of the line up to the first lower case / symbol command.
                    
                    int headerEnd = -1;
                    for(int c=0; c<line.Length; c++)
                    {
                        char ch = line[c];
                        if (char.IsWhiteSpace(ch)) continue;
                        if ((ch >= 'A' && ch <= 'H') || ch == 'P' || (ch >= 'a' && ch <= 'h'))
                        {
                            // It's a track letter
                        }
                        else 
                        {
                            headerEnd = c;
                            break;
                        }
                    }

                    if (headerEnd > 0)
                    {
                        string header = line.Substring(0, headerEnd);
                        string remainder = line.Substring(headerEnd);

                        // Build the new header character by character to avoid nested replacements
                        var newHeader = new System.Text.StringBuilder();

                        foreach (char ch in header)
                        {
                            string chStr = ch.ToString();
                            string upperCh = chStr.ToUpper();
                            
                            // If this character is one of the channels to be remapped
                            if (result.TryGetValue(upperCh, out string newCh))
                            {
                                // Keep original casing if it was lower case
                                if (char.IsLower(ch))
                                {
                                    newHeader.Append(newCh.ToLower());
                                }
                                else
                                {
                                    newHeader.Append(newCh);
                                }
                            }
                            else
                            {
                                // Pass through spaces and unmodified channels unaffected
                                newHeader.Append(ch);
                            }
                        }

                        lines[i] = newHeader.ToString() + remainder;
                    }
                }
                
                MmlInput.Text = string.Join(System.Environment.NewLine, lines);
                LogOutput.Text = "Channels remapped successfully.";
            }
            catch (System.Exception ex)
            {
                LogOutput.Text = $"Remap Error: {ex.Message}";
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
                int selStart = MmlInput.SelectionLength > 0 ? MmlInput.SelectionStart : -1;
                int selLen = MmlInput.SelectionLength > 0 ? MmlInput.SelectionLength : -1;
                
                _playbackTimer.Start();
                string log = await _player.PlayMmlAsync(mml, selStart, selLen);
                LogOutput.Text = log;
            }
            catch (System.Exception ex)
            {
                LogOutput.Text = $"MML Parse Error: {ex.Message}";
            }
            finally
            {
                _playbackTimer.Stop();
                ClearHighlight();
                btn.IsEnabled = true;
            }
        }
    }

    private void PlaybackTimer_Tick(object? sender, System.EventArgs e)
    {
        double currentMs = _player.CurrentPlaybackTimeMs;
        
        // Find the active text highlight events
        var activeEvents = _player.HighlightTimeline
            .Where(evt => currentMs >= evt.StartMs && currentMs < evt.EndMs)
            .ToList();

        // System.Diagnostics.Debug.WriteLine($"[Highlight] Time: {currentMs:F1}ms, ActiveCount: {activeEvents.Count}, TotalTimeline: {_player.HighlightTimeline.Count}");
        // Log to UI momentarily for testing
        if (activeEvents.Count > 0) 
        {
            // Try to avoid excessive UI lag, only update if needed or limit rate
        }

        if (activeEvents.Any())
        {
            var newSegments = activeEvents.Select(e => (e.TextStartIndex, e.TextLength)).ToList();
            
            // Basic equality check to avoid over-invalidating
            bool changed = false;
            if (_highlightRenderer.ActiveSegments.Count != newSegments.Count)
            {
                changed = true;
            }
            else
            {
                for (int i = 0; i < newSegments.Count; i++)
                {
                    if (_highlightRenderer.ActiveSegments[i].Offset != newSegments[i].TextStartIndex ||
                        _highlightRenderer.ActiveSegments[i].Length != newSegments[i].TextLength)
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                _highlightRenderer.ActiveSegments = newSegments;
                MmlInput.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);
            }
        }
        else if (_highlightRenderer.ActiveSegments.Count > 0)
        {
            ClearHighlight();
        }
    }

    private void ClearHighlight()
    {
        if (_highlightRenderer.ActiveSegments.Count > 0)
        {
            _highlightRenderer.ActiveSegments.Clear();
            MmlInput.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);
        }
    }

    private void StopButton_Click(object? sender, RoutedEventArgs e)
    {
        _player.Stop();
        _playbackTimer.Stop();
        ClearHighlight();
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