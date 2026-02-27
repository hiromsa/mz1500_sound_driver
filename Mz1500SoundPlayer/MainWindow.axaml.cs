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
    private int _currentVolumeA;
    public int CurrentVolumeA { get => _currentVolumeA; set => SetProperty(ref _currentVolumeA, value); }
    private int _currentVolumeB;
    public int CurrentVolumeB { get => _currentVolumeB; set => SetProperty(ref _currentVolumeB, value); }
    private int _currentVolumeC;
    public int CurrentVolumeC { get => _currentVolumeC; set => SetProperty(ref _currentVolumeC, value); }
    private int _currentVolumeD;
    public int CurrentVolumeD { get => _currentVolumeD; set => SetProperty(ref _currentVolumeD, value); }
    private int _currentVolumeE;
    public int CurrentVolumeE { get => _currentVolumeE; set => SetProperty(ref _currentVolumeE, value); }
    private int _currentVolumeF;
    public int CurrentVolumeF { get => _currentVolumeF; set => SetProperty(ref _currentVolumeF, value); }
    private int _currentVolumeG;
    public int CurrentVolumeG { get => _currentVolumeG; set => SetProperty(ref _currentVolumeG, value); }
    private int _currentVolumeH;
    public int CurrentVolumeH { get => _currentVolumeH; set => SetProperty(ref _currentVolumeH, value); }
    private int _currentVolumeP;
    public int CurrentVolumeP { get => _currentVolumeP; set => SetProperty(ref _currentVolumeP, value); }

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
        if (sender is Control btn)
        {
            ValidateMml();
            if (_errorRenderer.ActiveErrors.Count > 0)
            {
                LogOutput.Text = "エラーがあるため再生できません。修正してください。";
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

        // --- Volume Polling Section ---
        var volumes = _player.GetCurrentVolumes();
        CurrentVolumeA = volumes.TryGetValue("A", out var va) ? va : 0;
        CurrentVolumeB = volumes.TryGetValue("B", out var vb) ? vb : 0;
        CurrentVolumeC = volumes.TryGetValue("C", out var vc) ? vc : 0;
        CurrentVolumeD = volumes.TryGetValue("D", out var vd) ? vd : 0;
        CurrentVolumeE = volumes.TryGetValue("E", out var ve) ? ve : 0;
        CurrentVolumeF = volumes.TryGetValue("F", out var vf) ? vf : 0;
        CurrentVolumeG = volumes.TryGetValue("G", out var vg) ? vg : 0;
        CurrentVolumeH = volumes.TryGetValue("H", out var vh) ? vh : 0;
        CurrentVolumeP = volumes.TryGetValue("P", out var vp) ? vp : 0;
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
            LogOutput.Text = $"文法エラー: {error.Message}";
        }
        else if (LogOutput.Text?.StartsWith("文法エラー:") == true)
        {
            LogOutput.Text = "Ready";
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
        LogOutput.Text = "Playback stopped.";
    }

    private void ResetVolumes()
    {
        CurrentVolumeA = 0;
        CurrentVolumeB = 0;
        CurrentVolumeC = 0;
        CurrentVolumeD = 0;
        CurrentVolumeE = 0;
        CurrentVolumeF = 0;
        CurrentVolumeG = 0;
        CurrentVolumeH = 0;
        CurrentVolumeP = 0;
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
                LogOutput.Text = "Clipboard not available.";
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
            LogOutput.Text = $"No image found on clipboard. Available formats: {string.Join(", ", formats)}";
        }
        catch (System.Exception ex)
        {
            LogOutput.Text = $"Paste error: {ex.Message}";
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
            LogOutput.Text = $"Loaded PCG Image: {System.IO.Path.GetFileName(path)}";
        }
        catch (System.Exception ex)
        {
            LogOutput.Text = $"Failed to load image: {ex.Message}";
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
        LogOutput.Text = "PCG Image cleared.";
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
        var menuNewVolEnv = this.FindControl<MenuItem>("MenuNewVolEnvelope");
        var menuNewPitchEnv = this.FindControl<MenuItem>("MenuNewPitchEnvelope");

        if (menuEditEnv == null || menuNewVolEnv == null || menuNewPitchEnv == null) return;

        // カーソル行のテキストを取得
        var document = MmlInput.Document;
        var caretOffset = MmlInput.CaretOffset;
        if (caretOffset < 0 || caretOffset > document.TextLength) return;

        var line = document.GetLineByOffset(caretOffset);
        string lineText = document.GetText(line).Trim();

        // エンベロープ定義行かどうかの判定 (簡便に)
        bool isEnvelopeLine = Regex.IsMatch(lineText, @"^@(v|EP)\d+\s*=");

        if (isEnvelopeLine)
        {
            menuEditEnv.IsEnabled = true;
            menuEditEnv.Header = "カーソル行のエンベロープを編集...";
            
            menuNewVolEnv.IsEnabled = false;
            menuNewPitchEnv.IsEnabled = false;
        }
        else if (string.IsNullOrEmpty(lineText))
        {
            // 空行なら新規作成可能
            menuEditEnv.IsEnabled = false;
            menuEditEnv.Header = "カーソル行のエンベロープを編集... (空行)";

            menuNewVolEnv.IsEnabled = true;
            menuNewPitchEnv.IsEnabled = true;
        }
        else
        {
            // 上記以外（MMLデータ等）
            menuEditEnv.IsEnabled = false;
            menuEditEnv.Header = "カーソル行のエンベロープを編集... (無効な行)";
            
            menuNewVolEnv.IsEnabled = false;
            menuNewPitchEnv.IsEnabled = false;
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
}