using System;
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

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mz1500SoundPlayer;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly MmlPlayerModel _player;
    private readonly PlaybackHighlightRenderer _highlightRenderer;
    private readonly ErrorHighlightRenderer _errorRenderer;
    private readonly DispatcherTimer _playbackTimer;
    private readonly DispatcherTimer _validationTimer;

    // View Model Properties for UI binding
    private int _currentVolumeB11;
    public int CurrentVolumeB11 { get => _currentVolumeB11; set => SetProperty(ref _currentVolumeB11, value); }
    private int _currentVolumeB12;
    public int CurrentVolumeB12 { get => _currentVolumeB12; set => SetProperty(ref _currentVolumeB12, value); }
    private int _currentVolumeB13;
    public int CurrentVolumeB13 { get => _currentVolumeB13; set => SetProperty(ref _currentVolumeB13, value); }
    private int _currentVolumeN1;
    public int CurrentVolumeN1 { get => _currentVolumeN1; set => SetProperty(ref _currentVolumeN1, value); }
    private int _currentVolumeB14;
    public int CurrentVolumeB14 { get => _currentVolumeB14; set => SetProperty(ref _currentVolumeB14, value); }
    private int _currentVolumeB15;
    public int CurrentVolumeB15 { get => _currentVolumeB15; set => SetProperty(ref _currentVolumeB15, value); }
    private int _currentVolumeB16;
    public int CurrentVolumeB16 { get => _currentVolumeB16; set => SetProperty(ref _currentVolumeB16, value); }
    private int _currentVolumeN2;
    public int CurrentVolumeN2 { get => _currentVolumeN2; set => SetProperty(ref _currentVolumeN2, value); }
    private int _currentVolumeB1;
    public int CurrentVolumeB1 { get => _currentVolumeB1; set => SetProperty(ref _currentVolumeB1, value); }

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void SetProperty<T>(ref T backingField, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value)) return;
        backingField = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public MainWindow()
    {
        InitializeComponent();
        
        try
        {
            var buildDate = System.IO.File.GetLastWriteTime(System.Reflection.Assembly.GetExecutingAssembly().Location);
            this.Title = $"Mz1500SoundPlayer - Build: {buildDate:yyyy/MM/dd HH:mm:ss}";
        }
        catch { }

        this.DataContext = this;
        _player = new MmlPlayerModel();
        
        // Setup Highlight Renderer
        _highlightRenderer = new PlaybackHighlightRenderer();
        MmlInput.TextArea.TextView.BackgroundRenderers.Add(_highlightRenderer);

        _errorRenderer = new ErrorHighlightRenderer();
        MmlInput.TextArea.TextView.BackgroundRenderers.Add(_errorRenderer);

        // Setup Playback Timer for UI updates (~30fps)
        _playbackTimer = new DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(33)
        };
        _playbackTimer.Tick += PlaybackTimer_Tick;

        _validationTimer = new DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(500)
        };
        _validationTimer.Tick += (s, e) => { _validationTimer.Stop(); ValidateMml(); };

        MmlInput.TextChanged += (s, e) =>
        {
            _validationTimer.Stop();
            _validationTimer.Start();
        };

        MmlInput.TextArea.Caret.PositionChanged += TextArea_Caret_PositionChanged;
        
        // アプリ終了時に確実に音を止めるための処理
        this.Closed += (s, e) => _player.Stop();

        // テキストエリア等でイベントが消費される前にCaptureするため、Tunnel戦略でWindow全体にフックする
        this.AddHandler(InputElement.KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);

        // Editor Shortcuts
        MmlInput.TextArea.KeyDown += MmlInput_KeyDown;

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
        
        UpdateChannelMask();
    }

    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private async void LoadMidiButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control btn)
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
                        LogTextBox.Text = $"Loaded {files[0].Name} successfully.";
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogTextBox.Text = $"MIDI Load Error: {ex.Message}";
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }

    private async void LoadFmsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control btn)
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
                            LogTextBox.Text = $"Loaded {files[0].Name} successfully.";
                        }
                        else
                        {
                            LogTextBox.Text = $"Failed to parse {files[0].Name}. Error loading .fms";
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogTextBox.Text = $"FMS Load Error: {ex.Message}";
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }

    private async void LoadMmlButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control btn)
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
                        LogTextBox.Text = $"Loaded MML: {files[0].Name}";
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogTextBox.Text = $"MML Load Error: {ex.Message}";
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }

    private async void SaveMmlButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control btn)
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
                        LogTextBox.Text = $"Saved MML to: {file.Name}";
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogTextBox.Text = $"MML Save Error: {ex.Message}";
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
        
        var usedChannels = new HashSet<string>();
        var linesForScan = text.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        var trackHeaderRegex = new Regex(@"^\s*([A-Ha-hP]+)(?=\s|$)");

        foreach (var line in linesForScan)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith(";") || line.TrimStart().StartsWith("/"))
                continue;

            var match = trackHeaderRegex.Match(line);
            if (match.Success)
            {
                string tracks = match.Groups[1].Value.ToUpperInvariant();
                foreach (char ch in tracks)
                {
                    usedChannels.Add(ch.ToString());
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
                
                // We use Regex to match ONLY track definitions at the start of a line or block
                // Specifically matching characters A-H, P that stand alone or are followed by spaces.
                var lines = text.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
                var trackHeaderRegexEdit = new Regex(@"^(\s*)([A-Ha-hP]+)(?=\s|$)");

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    
                    // Skip comments
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith(";") || line.TrimStart().StartsWith("/"))
                        continue;

                    var match = trackHeaderRegexEdit.Match(line);
                    if (match.Success)
                    {
                        string prefixSpaces = match.Groups[1].Value;
                        string header = match.Groups[2].Value;
                        string remainder = line.Substring(match.Length);

                        // Build the new header character by character to avoid nested replacements
                        var newHeader = new System.Text.StringBuilder();

                        foreach (char ch in header)
                        {
                            string upperCh = char.ToUpper(ch).ToString();
                            
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
                                // Pass through unmodified channels unaffected
                                newHeader.Append(ch);
                            }
                        }

                        lines[i] = prefixSpaces + newHeader.ToString() + remainder;
                    }
                }
                
                MmlInput.Text = string.Join(System.Environment.NewLine, lines);
                LogTextBox.Text = "Channels remapped successfully.";
            }
            catch (System.Exception ex)
            {
                LogTextBox.Text = $"Remap Error: {ex.Message}";
            }
        }
    }

    private void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        MmlInput.Text = "";
    }

    private async void PlayMmlButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control btn)
        {
            ValidateMml();
            if (_errorRenderer.ActiveErrors.Count > 0)
            {
                LogTextBox.Text = "エラーがあるため再生できません。修正してください。";
                return;
            }

            btn.IsEnabled = false;
            try
            {
                string mml = MmlInput.Text ?? "";
                int selStart = MmlInput.SelectionLength > 0 ? MmlInput.SelectionStart : -1;
                int selLen = MmlInput.SelectionLength > 0 ? MmlInput.SelectionLength : -1;
                
                _playbackTimer.Start();
                string log = await _player.PlayMmlAsync(mml, selStart, selLen);
                LogTextBox.Text = log;
            }
            catch (System.Exception ex)
            {
                LogTextBox.Text = $"MML Parse Error: {ex.Message}";
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

        // --- Volume Polling Section ---
        var volumes = _player.GetCurrentVolumes();
        CurrentVolumeB11 = volumes.TryGetValue("A", out var va) ? va : 0;
        CurrentVolumeB12 = volumes.TryGetValue("B", out var vb) ? vb : 0;
        CurrentVolumeB13 = volumes.TryGetValue("C", out var vc) ? vc : 0;
        CurrentVolumeN1 = volumes.TryGetValue("D", out var vd) ? vd : 0;
        CurrentVolumeB14 = volumes.TryGetValue("E", out var ve) ? ve : 0;
        CurrentVolumeB15 = volumes.TryGetValue("F", out var vf) ? vf : 0;
        CurrentVolumeB16 = volumes.TryGetValue("G", out var vg) ? vg : 0;
        CurrentVolumeN2 = volumes.TryGetValue("H", out var vh) ? vh : 0;
        CurrentVolumeB1 = volumes.TryGetValue("P", out var vp) ? vp : 0;
        // ------------------------------

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

    private void ValidateMml()
    {
        string text = MmlInput.Text ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            _errorRenderer.ActiveErrors.Clear();
            MmlInput.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);
            return;
        }

        var parser = new MultiTrackMmlParser();
        var data = parser.Parse(text);
        
        _errorRenderer.ActiveErrors = data.Errors;
        MmlInput.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);

        // Update error message if caret is already on an error
        UpdateErrorMessageFromCaret();
    }

    private void TextArea_Caret_PositionChanged(object? sender, System.EventArgs e)
    {
        UpdateErrorMessageFromCaret();
    }

    private void UpdateErrorMessageFromCaret()
    {
        int offset = MmlInput.CaretOffset;
        var error = _errorRenderer.ActiveErrors.FirstOrDefault(err => 
            offset >= err.TextStartIndex && offset <= err.TextStartIndex + err.Length);

        if (error != null)
        {
            LogTextBox.Text = $"文法エラー: {error.Message}";
        }
        else if (LogTextBox.Text?.StartsWith("文法エラー:") == true)
        {
            LogTextBox.Text = "Ready";
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
        ResetVolumes();
        LogTextBox.Text = "Playback stopped.";
    }

    private void ResetVolumes()
    {
        CurrentVolumeB11 = 0;
        CurrentVolumeB12 = 0;
        CurrentVolumeB13 = 0;
        CurrentVolumeN1 = 0;
        CurrentVolumeB14 = 0;
        CurrentVolumeB15 = 0;
        CurrentVolumeB16 = 0;
        CurrentVolumeN2 = 0;
        CurrentVolumeB1 = 0;
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

    private async void PastePcgImageButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
            {
                LogTextBox.Text = "Clipboard not available.";
                return;
            }

            // 1. File copied from Explorer
            var files = await clipboard.GetDataAsync(Avalonia.Input.DataFormats.Files) as System.Collections.Generic.IEnumerable<Avalonia.Platform.Storage.IStorageItem>;
            if (files != null && files.Any())
            {
                string path = files.First().Path.LocalPath;
                await LoadImageFromPath(path);
                return;
            }

            // 2. PNG block (e.g. from browser right-click copy image)
            string? pngTempPath = null;
            foreach (var format in new[] { "PNG", "image/png", "PNG Format" })
            {
                var pngData = await clipboard.GetDataAsync(format) as byte[];
                if (pngData != null && pngData.Length > 0)
                {
                    pngTempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcg_paste_tmp.png");
                    await System.IO.File.WriteAllBytesAsync(pngTempPath, pngData);
                    break;
                }
            }

            if (pngTempPath != null && System.IO.File.Exists(pngTempPath))
            {
                await LoadImageFromPath(pngTempPath);
                return;
            }

            // 3. DeviceIndependentBitmap (スクリーンショット等)
            // Windows DIB形式: 40バイトのBITMAPINFOHEADERが先頭にある
            var dibData = await clipboard.GetDataAsync("DeviceIndependentBitmap") as byte[];
            if (dibData != null && dibData.Length > 40)
            {
                string tmpPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcg_paste_tmp_dib.bmp");
                // BMPファイルとして保存 (14バイトのBMPFILEHEADERを先頭に追加)
                int fileSize = 14 + dibData.Length;
                int pixelDataOffset = 14 + 40; // FH + BITMAPINFOHEADER size
                // Check color table based on bit count
                int biBitCount = System.BitConverter.ToInt16(dibData, 14);
                if (biBitCount <= 8) pixelDataOffset += (1 << biBitCount) * 4;

                using var ms = new System.IO.MemoryStream(fileSize);
                // BITMAPFILEHEADER
                ms.WriteByte((byte)'B'); ms.WriteByte((byte)'M'); // Signature
                ms.Write(System.BitConverter.GetBytes(fileSize), 0, 4); // File size
                ms.Write(new byte[4], 0, 4);                     // Reserved
                ms.Write(System.BitConverter.GetBytes(pixelDataOffset), 0, 4); // Pixel data offset
                ms.Write(dibData, 0, dibData.Length);
                await System.IO.File.WriteAllBytesAsync(tmpPath, ms.ToArray());
                await LoadImageFromPath(tmpPath);
                return;
            }

            // Nothing found
            var formats = await clipboard.GetFormatsAsync();
            LogTextBox.Text = $"No image found on clipboard. Available formats: {string.Join(", ", formats)}";
        }
        catch (System.Exception ex)
        {
            LogTextBox.Text = $"Paste error: {ex.Message}";
        }
    }

    private async Task LoadImageFromPath(string path)
    {
        try
        {
            var bitmap = new Avalonia.Media.Imaging.Bitmap(path);
            PcgImagePreview.Source = bitmap;
            if (_player != null)
            {
                _player.PcgImagePath = path;
            }
            LogTextBox.Text = $"Loaded PCG Image: {System.IO.Path.GetFileName(path)}";
        }
        catch (System.Exception ex)
        {
            LogTextBox.Text = $"Failed to load image: {ex.Message}";
        }
    }

    private async void LoadPcgImageButton_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new Avalonia.Controls.OpenFileDialog
        {
            Title = "Load PCG Image",
            AllowMultiple = false,
            Filters = new List<Avalonia.Controls.FileDialogFilter>
            {
                new Avalonia.Controls.FileDialogFilter { Name = "Image Files", Extensions = { "png", "jpg", "jpeg", "bmp" } }
            }
        };

        var result = await dialog.ShowAsync(this);
        if (result != null && result.Length > 0)
        {
            await LoadImageFromPath(result[0]);
        }
    }

    private void ClearPcgImageButton_Click(object? sender, RoutedEventArgs e)
    {
        PcgImagePreview.Source = null;
        if (_player != null)
        {
            _player.PcgImagePath = null;
        }
        LogTextBox.Text = "PCG Image cleared.";
    }

    private void ExportQdcButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string mml = MmlInput.Text ?? "";
            
            // 本当はSaveFileDialogを使うべきだが、簡便化のため実行ファイルと同じ場所に固定で吐き出す
            string outPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "output.qdc");
            
            string log = _player.ExportQdc(mml, outPath);
            LogTextBox.Text = log;
        }
        catch (System.Exception ex)
        {
            LogTextBox.Text = $"Export Error: {ex.Message}";
        }
    }

    private bool _isUpdatingCheckboxes = false;
    private void ChkB11ll_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingCheckboxes) return;
        _isUpdatingCheckboxes = true;
        bool isChecked = ChkB11ll.IsChecked ?? false;
        ChkB11.IsChecked = isChecked;
        ChkB12.IsChecked = isChecked;
        ChkB13.IsChecked = isChecked;
        ChkN1.IsChecked = isChecked;
        ChkB14.IsChecked = isChecked;
        ChkB15.IsChecked = isChecked;
        ChkB16.IsChecked = isChecked;
        ChkN2.IsChecked = isChecked;
        ChkB1.IsChecked = isChecked;
        _isUpdatingCheckboxes = false;
        UpdateChannelMask();
    }

    private void ChkB13hannel_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingCheckboxes) return;
        _isUpdatingCheckboxes = true;
        
        bool allChecked = (ChkB11.IsChecked == true) && (ChkB12.IsChecked == true) && (ChkB13.IsChecked == true) && (ChkN1.IsChecked == true) && (ChkB14.IsChecked == true) && (ChkB15.IsChecked == true) && (ChkB16.IsChecked == true) && (ChkN2.IsChecked == true) && (ChkB1.IsChecked == true);
        ChkB11ll.IsChecked = allChecked;
        
        _isUpdatingCheckboxes = false;
        UpdateChannelMask();
    }

    private void UpdateChannelMask()
    {
        if (_player == null) return;
        var activeChannels = new System.Collections.Generic.HashSet<string>();
        if (ChkB11.IsChecked == true) activeChannels.Add("P1");
        if (ChkB12.IsChecked == true) activeChannels.Add("P2");
        if (ChkB13.IsChecked == true) activeChannels.Add("P3");
        if (ChkN1.IsChecked == true) activeChannels.Add("N1");
        if (ChkB14.IsChecked == true) activeChannels.Add("P4");
        if (ChkB15.IsChecked == true) activeChannels.Add("P5");
        if (ChkB16.IsChecked == true) activeChannels.Add("P6");
        if (ChkN2.IsChecked == true) activeChannels.Add("N2");
        if (ChkB1.IsChecked == true) activeChannels.Add("B1");
        if (ChkB11ll.IsChecked == true) 
        {
            activeChannels.Add("F1"); activeChannels.Add("F2"); activeChannels.Add("F3"); activeChannels.Add("F4");
            activeChannels.Add("F5"); activeChannels.Add("F6"); activeChannels.Add("F7"); activeChannels.Add("F8");
        }
        
        _player.ActiveChannels = activeChannels;
    }

    private void ChkMetronome_Changed(object? sender, RoutedEventArgs e)
    {
        if (_player != null && ChkMetronome != null)
        {
            _player.IsMetronomeActive = ChkMetronome.IsChecked ?? false;
        }
    }

    private async void EditMmlSelection_Click(object? sender, RoutedEventArgs e)
    {
        await OpenEditorWindowAsync();
    }

    private async void FormatMmlSelection_Click(object? sender, RoutedEventArgs e)
    {
        if (MmlInput.SelectionLength == 0) return;

        int start = MmlInput.SelectionStart;
        int length = MmlInput.SelectionLength;
        string selectedText = MmlInput.SelectedText;

        var formatterWindow = new MmlFormatterWindow(selectedText);
        var result = await formatterWindow.ShowDialog<string>(this);

        if (result != null)
        {
            MmlInput.Document.Replace(start, length, result);
            MmlInput.Select(start, result.Length);
        }
    }

    private void MmlInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.E)
        {
            e.Handled = true;
            _ = OpenEditorWindowAsync();
        }
    }

    private async Task OpenEditorWindowAsync()
    {
        if (MmlInput.SelectionLength == 0) return;

        int start = MmlInput.SelectionStart;
        int length = MmlInput.SelectionLength;
        string selectedText = MmlInput.SelectedText;

        string prefixContext = MmlInput.Text.Substring(0, start);

        var editorWindow = new MmlEditorWindow(selectedText, prefixContext);
        var result = await editorWindow.ShowDialog<string>(this);

        if (result != null)
        {
            MmlInput.Document.Replace(start, length, result);
            MmlInput.Select(start, result.Length);
        }
    }

    private void MmlInput_ContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var menuEditEnv = this.FindControl<MenuItem>("MenuEditEnvelope");
        var menuEditFm = this.FindControl<MenuItem>("MenuEditFmVoice");
        var menuNewVolEnv = this.FindControl<MenuItem>("MenuNewVolEnvelope");
        var menuNewPitchEnv = this.FindControl<MenuItem>("MenuNewPitchEnvelope");
        var menuNewFmVoice = this.FindControl<MenuItem>("MenuNewFmVoice");

        if (menuEditEnv == null || menuEditFm == null || menuNewVolEnv == null || menuNewPitchEnv == null || menuNewFmVoice == null) return;

        // カーソル行のテキストを取得
        var document = MmlInput.Document;
        var caretOffset = MmlInput.CaretOffset;
        if (caretOffset < 0 || caretOffset > document.TextLength) return;

        var line = document.GetLineByOffset(caretOffset);
        string lineText = document.GetText(line).Trim();

        // エンベロープ定義行かどうかの判定 (簡便に)
        bool isEnvelopeLine = Regex.IsMatch(lineText, @"^@(v|EP)\d+\s*=");
        // FM音色かどうかの判定 (複数行にまたがるケースも考慮して検索する)
        bool isFmVoice = false;
        
        int startLineNum = line.LineNumber;
        while (startLineNum > 0)
        {
            var testLine = document.GetLineByNumber(startLineNum);
            string testLineText = document.GetText(testLine).Trim();
            if (Regex.IsMatch(testLineText, @"^@FM\[?\d+\]?\s*="))
            {
                int startOffset = testLine.Offset;
                int searchLen = Math.Min(2000, document.TextLength - startOffset);
                string searchStr = document.GetText(startOffset, searchLen);
                int openBrace = searchStr.IndexOf('{');
                int closeBrace = searchStr.IndexOf('}');
                if (openBrace != -1 && closeBrace != -1 && closeBrace > openBrace)
                {
                    if (caretOffset >= startOffset && caretOffset <= startOffset + closeBrace)
                    {
                        isFmVoice = true;
                    }
                }
                break;
            }
            startLineNum--;
        }

        if (isEnvelopeLine)
        {
            menuEditEnv.IsEnabled = true;
            menuEditEnv.Header = "カーソル行のエンベロープを編集...";
            
            menuEditFm.IsEnabled = false;
            menuEditFm.Header = "FM音色エディタを開く... (無効な行)";

            menuNewVolEnv.IsEnabled = false;
            menuNewPitchEnv.IsEnabled = false;
            menuNewFmVoice.IsEnabled = false;
        }
        else if (isFmVoice)
        {
            menuEditEnv.IsEnabled = false;
            menuEditEnv.Header = "カーソル行のエンベロープを編集... (無効な行)";
            
            menuEditFm.IsEnabled = true;
            menuEditFm.Header = "カーソル行のFM音色を編集...";

            menuNewVolEnv.IsEnabled = false;
            menuNewPitchEnv.IsEnabled = false;
            menuNewFmVoice.IsEnabled = false;
        }
        else if (string.IsNullOrEmpty(lineText))
        {
            // 空行なら新規作成可能
            menuEditEnv.IsEnabled = false;
            menuEditEnv.Header = "カーソル行のエンベロープを編集... (空行)";
            
            menuEditFm.IsEnabled = false;
            menuEditFm.Header = "FM音色エディタを開く... (無効な行)";

            menuNewVolEnv.IsEnabled = true;
            menuNewPitchEnv.IsEnabled = true;
            menuNewFmVoice.IsEnabled = true;
        }
        else
        {
            // 上記以外（MMLデータ等）
            menuEditEnv.IsEnabled = false;
            menuEditEnv.Header = "カーソル行のエンベロープを編集... (無効な行)";
            
            menuEditFm.IsEnabled = false;
            menuEditFm.Header = "FM音色エディタを開く... (無効な行)";
            
            menuNewVolEnv.IsEnabled = false;
            menuNewPitchEnv.IsEnabled = false;
            menuNewFmVoice.IsEnabled = false;
        }
    }

    private int FindNextAvailableEnvelopeId(string prefix)
    {
        string text = MmlInput.Text ?? "";
        var regex = new Regex($@"@{prefix}(\d+)\s*=");
        var matches = regex.Matches(text);
        
        var usedIds = new HashSet<int>();
        foreach (Match m in matches)
        {
            if (int.TryParse(m.Groups[1].Value, out int id))
            {
                usedIds.Add(id);
            }
        }

        int nextId = 0;
        while (usedIds.Contains(nextId))
        {
            nextId++;
        }
        return nextId;
    }

    private void EditEnvelope_Click(object? sender, RoutedEventArgs e)
    {
        var document = MmlInput.Document;
        var line = document.GetLineByOffset(MmlInput.CaretOffset);
        string lineText = document.GetText(line).Trim();

        var match = Regex.Match(lineText, @"^@(v|EP)(\d+)\s*=\s*(.*)");
        if (match.Success)
        {
            string typeStr = match.Groups[1].Value;
            int id = int.Parse(match.Groups[2].Value);
            string data = match.Groups[3].Value;

            var type = typeStr == "v" ? EnvelopeEditorWindow.EnvelopeType.Volume : EnvelopeEditorWindow.EnvelopeType.Pitch;
            OpenEnvelopeEditor(type, id, data, line.Offset, line.Length);
        }
    }

    private void NewVolEnvelope_Click(object? sender, RoutedEventArgs e)
    {
        int nextId = FindNextAvailableEnvelopeId("v");
        var document = MmlInput.Document;
        var line = document.GetLineByOffset(MmlInput.CaretOffset);
        OpenEnvelopeEditor(EnvelopeEditorWindow.EnvelopeType.Volume, nextId, "", line.Offset, line.Length);
    }

    private void NewPitchEnvelope_Click(object? sender, RoutedEventArgs e)
    {
        int nextId = FindNextAvailableEnvelopeId("EP");
        var document = MmlInput.Document;
        var line = document.GetLineByOffset(MmlInput.CaretOffset);
        OpenEnvelopeEditor(EnvelopeEditorWindow.EnvelopeType.Pitch, nextId, "", line.Offset, line.Length);
    }

    private async void OpenEnvelopeEditor(EnvelopeEditorWindow.EnvelopeType type, int id, string existingData, int replaceOffset, int replaceLength)
    {
        // Get all used IDs to prevent overriding
        var prefix = type == EnvelopeEditorWindow.EnvelopeType.Volume ? "v" : "EP";
        string text = MmlInput.Text ?? "";
        var regex = new Regex($@"@{prefix}(\d+)\s*=");
        var matches = regex.Matches(text);
        var usedIds = new HashSet<int>();
        foreach (Match m in matches)
        {
            if (int.TryParse(m.Groups[1].Value, out int testId))
            {
                usedIds.Add(testId);
            }
        }

        var editor = new EnvelopeEditorWindow(type, id, existingData, usedIds);
        var result = await editor.ShowDialog<string>(this);

        if (!string.IsNullOrEmpty(result))
        {
            // Empty line means insertion, Existing line means replace
            var doc = MmlInput.Document;
            string currentLineText = doc.GetText(replaceOffset, replaceLength);
            
            if (string.IsNullOrWhiteSpace(currentLineText))
            {
                doc.Replace(replaceOffset, replaceLength, result);
            }
            else
            {
                // If the user modified the ID in the editor, ensure we update the line start too
                doc.Replace(replaceOffset, replaceLength, result);
            }
        }
    }

    private async void EditFmVoice_Click(object? sender, RoutedEventArgs e)
    {
        var document = MmlInput.Document;
        int caretOffset = MmlInput.CaretOffset;
        
        int startLineNum = document.GetLineByOffset(caretOffset).LineNumber;
        while (startLineNum > 0)
        {
            var line = document.GetLineByNumber(startLineNum);
            string lineText = document.GetText(line).Trim();
            var match = Regex.Match(lineText, @"^@FM\[?(\d+)\]?\s*=");
            if (match.Success)
            {
                int id = int.Parse(match.Groups[1].Value);
                int startOffset = line.Offset;
                int searchLen = Math.Min(2000, document.TextLength - startOffset);
                string searchStr = document.GetText(startOffset, searchLen);
                int closeBrace = searchStr.IndexOf('}');
                
                if (closeBrace != -1)
                {
                    string mmlBlock = searchStr.Substring(0, closeBrace + 1);
                    // Extract just the inside part if needed, or pass the whole block
                    // Wait, FmEditorWindow expects just the inner MML or the whole block?
                    // Currently it uses match.Groups[2].Value which is just the rest of the line (e.g. "{ 4, 3 ... }")
                    // Since FmEditorViewModel uses Regex to find "{([^}]+)}", we can pass the whole block.
                    
                    var editor = new FmEditorWindow(id, mmlBlock);
                    editor.OnApply = (newMml) =>
                    {
                        document.Replace(startOffset, closeBrace + 1, newMml);
                    };
                    await editor.ShowDialog(this);
                }
                break;
            }
            startLineNum--;
        }
    }

    private async void NewFmVoice_Click(object? sender, RoutedEventArgs e)
    {
        string text = MmlInput.Text ?? "";
        var regex = new Regex(@"@FM\[?(\d+)\]?\s*=");
        var matches = regex.Matches(text);
        
        var usedIds = new HashSet<int>();
        foreach (Match m in matches)
        {
            if (int.TryParse(m.Groups[1].Value, out int testId))
            {
                usedIds.Add(testId);
            }
        }

        int nextId = 0;
        while (usedIds.Contains(nextId))
        {
            nextId++;
        }

        var document = MmlInput.Document;
        var line = document.GetLineByOffset(MmlInput.CaretOffset);

        // Pass empty block to create default
        var editor = new FmEditorWindow(nextId, "{ }");
        editor.OnApply = (newMml) =>
        {
            document.Insert(line.Offset, newMml + "\r\n");
        };
        await editor.ShowDialog(this);
    }

    private void OpenVirtualKeyboard_Click(object? sender, RoutedEventArgs e)
    {
        int caretOffset = MmlInput.CaretOffset;
        string mmlText = MmlInput.Text ?? "";
        
        var parser = new MultiTrackMmlParser();
        var mmlData = parser.Parse(mmlText);

        // Find track name at caret line, or default to first track / F1
        var document = MmlInput.Document;
        var line = document.GetLineByOffset(caretOffset);
        string lineText = document.GetText(line).Trim();
        
        string targetTrack = "F1";
        foreach (var kvp in mmlData.Tracks)
        {
            if (lineText.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                targetTrack = kvp.Key;
                break;
            }
        }

        var expander = new TrackEventExpander();
        ChannelState state;
        if (mmlData.Tracks.TryGetValue(targetTrack, out var trackData))
        {
            state = expander.GetStateAtPosition(trackData, targetTrack, caretOffset);
        }
        else
        {
            state = new ChannelState(targetTrack, 4, 15, -1, -1, 0, 3, 127, 0, 0);
        }

        var keyboardWindow = new VirtualKeyboardWindow();
        keyboardWindow.InitializeState(state, mmlData);

        keyboardWindow.OnInsertMml = (insertedMml) =>
        {
            MmlInput.Document.Insert(MmlInput.CaretOffset, insertedMml);
        };

        keyboardWindow.Show(this);
    }
}
