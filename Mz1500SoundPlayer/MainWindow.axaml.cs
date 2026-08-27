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
    public FileTreeViewModel MmlLibrary { get; } = new("mmls", "*.mml");
    private readonly MmlPlayerModel _player;
    private readonly PlaybackHighlightRenderer _highlightRenderer;
    private readonly ErrorHighlightRenderer _errorRenderer;
    private readonly DispatcherTimer _playbackTimer;
    private readonly DispatcherTimer _validationTimer;

    // View Model Properties for UI binding
    private int _currentVolumeP1;
    public int CurrentVolumeP1 { get => _currentVolumeP1; set => SetProperty(ref _currentVolumeP1, value); }
    private int _currentVolumeP2;
    public int CurrentVolumeP2 { get => _currentVolumeP2; set => SetProperty(ref _currentVolumeP2, value); }
    private int _currentVolumeP3;
    public int CurrentVolumeP3 { get => _currentVolumeP3; set => SetProperty(ref _currentVolumeP3, value); }
    private int _currentVolumeN1;
    public int CurrentVolumeN1 { get => _currentVolumeN1; set => SetProperty(ref _currentVolumeN1, value); }
    private int _currentVolumeP4;
    public int CurrentVolumeP4 { get => _currentVolumeP4; set => SetProperty(ref _currentVolumeP4, value); }
    private int _currentVolumeP5;
    public int CurrentVolumeP5 { get => _currentVolumeP5; set => SetProperty(ref _currentVolumeP5, value); }
    private int _currentVolumeP6;
    public int CurrentVolumeP6 { get => _currentVolumeP6; set => SetProperty(ref _currentVolumeP6, value); }
    private int _currentVolumeN2;
    public int CurrentVolumeN2 { get => _currentVolumeN2; set => SetProperty(ref _currentVolumeN2, value); }
    private int _currentVolumeB1;
    public int CurrentVolumeB1 { get => _currentVolumeB1; set => SetProperty(ref _currentVolumeB1, value); }
    private int _currentVolumeF1;
    public int CurrentVolumeF1 { get => _currentVolumeF1; set => SetProperty(ref _currentVolumeF1, value); }
    private int _currentVolumeF2;
    public int CurrentVolumeF2 { get => _currentVolumeF2; set => SetProperty(ref _currentVolumeF2, value); }
    private int _currentVolumeF3;
    public int CurrentVolumeF3 { get => _currentVolumeF3; set => SetProperty(ref _currentVolumeF3, value); }
    private int _currentVolumeF4;
    public int CurrentVolumeF4 { get => _currentVolumeF4; set => SetProperty(ref _currentVolumeF4, value); }
    private int _currentVolumeF5;
    public int CurrentVolumeF5 { get => _currentVolumeF5; set => SetProperty(ref _currentVolumeF5, value); }
    private int _currentVolumeF6;
    public int CurrentVolumeF6 { get => _currentVolumeF6; set => SetProperty(ref _currentVolumeF6, value); }
    private int _currentVolumeF7;
    public int CurrentVolumeF7 { get => _currentVolumeF7; set => SetProperty(ref _currentVolumeF7, value); }
    private int _currentVolumeF8;
    public int CurrentVolumeF8 { get => _currentVolumeF8; set => SetProperty(ref _currentVolumeF8, value); }

    // Tab Properties
    public System.Collections.ObjectModel.ObservableCollection<MmlDocumentTab> MmlTabs { get; } = new();

    private MmlDocumentTab? _selectedMmlTab;
    public MmlDocumentTab? SelectedMmlTab
    {
        get => _selectedMmlTab;
        set
        {
            if (_selectedMmlTab != null)
            {
                _selectedMmlTab.CaretOffset = MmlInput.CaretOffset;
            }

            if (SetProperty(ref _selectedMmlTab, value))
            {
                if (_selectedMmlTab != null)
                {
                    MmlInput.Document = _selectedMmlTab.Document;
                    MmlInput.CaretOffset = System.Math.Min(_selectedMmlTab.CaretOffset, _selectedMmlTab.Document.TextLength);
                }
                else
                {
                    MmlInput.Document = new AvaloniaEdit.Document.TextDocument();
                }
            }
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    private bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value)) return false;
        backingField = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
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
            if (SelectedMmlTab != null) SelectedMmlTab.IsDirty = true;
        };

        MmlInput.TextArea.Caret.PositionChanged += TextArea_Caret_PositionChanged;

        // Init default tab
        var defaultTab = new MmlDocumentTab { Title = "untitled.mml" };
        defaultTab.Document.Text = MmlInput.Text ?? "";
        defaultTab.IsDirty = false;
        MmlTabs.Add(defaultTab);
        SelectedMmlTab = defaultTab;
        
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



    private void OpenFileFromTree(string filePath)
    {
        var existing = MmlTabs.FirstOrDefault(t => t.FilePath == filePath);
        if (existing != null)
        {
            SelectedMmlTab = existing;
            return;
        }

        var newTab = new MmlDocumentTab 
        { 
            Title = System.IO.Path.GetFileName(filePath),
            FilePath = filePath
        };
        try
        {
            newTab.Document.Text = System.IO.File.ReadAllText(filePath);
            newTab.IsDirty = false;
            MmlTabs.Add(newTab);
            SelectedMmlTab = newTab;
        }
        catch (Exception ex)
        {
            LogTextBox.Text = $"Error opening file: {ex.Message}";
        }
    }

    private void CloseMmlTab_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is MmlDocumentTab tab)
        {
            MmlTabs.Remove(tab);
            if (SelectedMmlTab == tab)
            {
                SelectedMmlTab = MmlTabs.FirstOrDefault();
            }
        }
    }

    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void NewMml_Click(object? sender, RoutedEventArgs e)
    {
        var newTab = new MmlDocumentTab();
        MmlTabs.Add(newTab);
        SelectedMmlTab = newTab;
    }

    private async void LoadMml_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "MMLを開く",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("MML Files") { Patterns = new[] { "*.mml", "*.txt" } } }
            });

            if (files.Count > 0)
            {
                OpenFileFromTree(files[0].Path.LocalPath);
            }
        }
    }

    private void SaveMml_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedMmlTab == null) return;

        if (string.IsNullOrEmpty(SelectedMmlTab.FilePath))
        {
            SaveAsMml_Click(sender, e);
        }
        else
        {
            try
            {
                System.IO.File.WriteAllText(SelectedMmlTab.FilePath, SelectedMmlTab.Document.Text);
                SelectedMmlTab.IsDirty = false;
                LogTextBox.Text = $"Saved to {System.IO.Path.GetFileName(SelectedMmlTab.FilePath)} successfully.";
                MmlLibrary.Refresh();
            }
            catch (System.Exception ex)
            {
                LogTextBox.Text = $"Failed to save MML: {ex.Message}";
            }
        }
    }

    private async void SaveAsMml_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedMmlTab == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "MMLを保存",
                SuggestedFileName = SelectedMmlTab.Title,
                DefaultExtension = ".mml",
                FileTypeChoices = new[] { new FilePickerFileType("MML Files") { Patterns = new[] { "*.mml" } } }
            });

            if (file != null)
            {
                try
                {
                    string path = file.Path.LocalPath;
                    System.IO.File.WriteAllText(path, SelectedMmlTab.Document.Text);
                    SelectedMmlTab.FilePath = path;
                    SelectedMmlTab.Title = System.IO.Path.GetFileName(path);
                    SelectedMmlTab.IsDirty = false;
                    LogTextBox.Text = $"Saved to {SelectedMmlTab.Title} successfully.";
                    MmlLibrary.Refresh();
                }
                catch (System.Exception ex)
                {
                    LogTextBox.Text = $"Failed to save MML: {ex.Message}";
                }
            }
        }
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
                        MmlLibrary.Refresh();
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
        CurrentVolumeP1 = volumes.TryGetValue("P1", out var va) ? va : 0;
        CurrentVolumeP2 = volumes.TryGetValue("P2", out var vb) ? vb : 0;
        CurrentVolumeP3 = volumes.TryGetValue("P3", out var vc) ? vc : 0;
        CurrentVolumeN1 = volumes.TryGetValue("N1", out var vd) ? vd : 0;
        CurrentVolumeP4 = volumes.TryGetValue("P4", out var ve) ? ve : 0;
        CurrentVolumeP5 = volumes.TryGetValue("P5", out var vf) ? vf : 0;
        CurrentVolumeP6 = volumes.TryGetValue("P6", out var vg) ? vg : 0;
        CurrentVolumeN2 = volumes.TryGetValue("N2", out var vh) ? vh : 0;
        CurrentVolumeB1 = volumes.TryGetValue("B1", out var vp) ? vp : 0;
        
        CurrentVolumeF1 = volumes.TryGetValue("F1", out var vf1) ? vf1 : 0;
        CurrentVolumeF2 = volumes.TryGetValue("F2", out var vf2) ? vf2 : 0;
        CurrentVolumeF3 = volumes.TryGetValue("F3", out var vf3) ? vf3 : 0;
        CurrentVolumeF4 = volumes.TryGetValue("F4", out var vf4) ? vf4 : 0;
        CurrentVolumeF5 = volumes.TryGetValue("F5", out var vf5) ? vf5 : 0;
        CurrentVolumeF6 = volumes.TryGetValue("F6", out var vf6) ? vf6 : 0;
        CurrentVolumeF7 = volumes.TryGetValue("F7", out var vf7) ? vf7 : 0;
        CurrentVolumeF8 = volumes.TryGetValue("F8", out var vf8) ? vf8 : 0;
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
        CurrentVolumeP1 = 0;
        CurrentVolumeP2 = 0;
        CurrentVolumeP3 = 0;
        CurrentVolumeN1 = 0;
        CurrentVolumeP4 = 0;
        CurrentVolumeP5 = 0;
        CurrentVolumeP6 = 0;
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

    private async void EmulatorRun_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new Avalonia.Controls.OpenFileDialog
        {
            Title = "Run Emulator File",
            AllowMultiple = false,
            Filters = new List<Avalonia.Controls.FileDialogFilter>
            {
                new Avalonia.Controls.FileDialogFilter { Name = "MZ-1500 Files", Extensions = { "mzt", "qdf" } }
            }
        };

        var result = await dialog.ShowAsync(this);
        if (result != null && result.Length > 0)
        {
            string path = result[0];
            string ext = System.IO.Path.GetExtension(path).ToLower();
            
            LogTextBox.Text = $"Loading {path} in emulator...\n";
            await System.Threading.Tasks.Task.Delay(10); // Force UI update

            LogTextBox.Text += "[1] Creating Mz1500Machine...\n";
            await System.Threading.Tasks.Task.Delay(10);
            var machine = new Sound.Emulator.Mz1500Machine();
            
            LogTextBox.Text += "[2] Machine created OK.\n";
            await System.Threading.Tasks.Task.Delay(10);
            
            if (ext == ".mzt")
            {
                LogTextBox.Text += "[3] Loading MZT...\n";
                await System.Threading.Tasks.Task.Delay(10);
                if (machine.LoadMzt(path))
                {
                    LogTextBox.Text += "[4] MZT loaded. Creating EmulatorWindow...\n";
                    await System.Threading.Tasks.Task.Delay(10);
                    var emulatorWin = new EmulatorWindow();
                    
                    LogTextBox.Text += "[5] Showing window...\n";
                    await System.Threading.Tasks.Task.Delay(10);
                    emulatorWin.Show();
                    
                    var debuggerWin = new DebuggerWindow();
                    debuggerWin.Show();
                    debuggerWin.SetMachine(machine);
                    
                    LogTextBox.Text += "[6] Calling Start...\n";
                    await System.Threading.Tasks.Task.Delay(10);
                    emulatorWin.Start(machine);
                    
                    LogTextBox.Text += "[7] Starting CPU on background thread...\n";
                    await System.Threading.Tasks.Task.Delay(10);
                    _ = System.Threading.Tasks.Task.Run(() => machine.Run());
                    
                    LogTextBox.Text += "[8] All done. Emulator should be running.\n";
                }
                else
                {
                    LogTextBox.Text += "Failed to load MZT.\n";
                }
            }
            else if (ext == ".qdf")
            {
                LogTextBox.Text += "[3] Loading QDF...\n";
                await System.Threading.Tasks.Task.Delay(10);
                if (machine.LoadQdf(path))
                {
                    LogTextBox.Text += "[4] QDF loaded. Creating EmulatorWindow...\n";
                    await System.Threading.Tasks.Task.Delay(10);
                    var emulatorWin = new EmulatorWindow();
                    
                    LogTextBox.Text += "[5] Showing window...\n";
                    await System.Threading.Tasks.Task.Delay(10);
                    emulatorWin.Show();
                    
                    var debuggerWin = new DebuggerWindow();
                    debuggerWin.Show();
                    debuggerWin.SetMachine(machine);
                    
                    LogTextBox.Text += "[6] Calling Start...\n";
                    await System.Threading.Tasks.Task.Delay(10);
                    emulatorWin.Start(machine);
                    
                    LogTextBox.Text += "[7] Starting CPU on background thread...\n";
                    await System.Threading.Tasks.Task.Delay(10);
                    _ = System.Threading.Tasks.Task.Run(() => machine.Run());
                    
                    LogTextBox.Text += "[8] All done. Emulator should be running.\n";
                }
                else
                {
                    LogTextBox.Text += "Failed to load QDF.\n";
                }
            }
        }
        }

        private async void EmulatorRunNoMedia_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Text += "Running MZ-1500 without media...\n";
            var machine = new Sound.Emulator.Mz1500Machine();
            
            var emulatorWin = new EmulatorWindow();
            emulatorWin.Show();
            emulatorWin.Start(machine);
            
            var debuggerWin = new DebuggerWindow();
            debuggerWin.Show();
            debuggerWin.SetMachine(machine);
            
            _ = System.Threading.Tasks.Task.Run(() => machine.Run());
        }
    private void ChkB13hannel_Changed(object? sender, RoutedEventArgs e)
    {
        UpdateChannelMask();
    }

    private void UpdateChannelMask()
    {
        if (_player == null) return;
        var activeChannels = new System.Collections.Generic.HashSet<string>();
        
        if (ChkP1?.IsChecked == true) activeChannels.Add("P1");
        if (ChkP2?.IsChecked == true) activeChannels.Add("P2");
        if (ChkP3?.IsChecked == true) activeChannels.Add("P3");
        if (ChkP4?.IsChecked == true) activeChannels.Add("P4");
        if (ChkP5?.IsChecked == true) activeChannels.Add("P5");
        if (ChkP6?.IsChecked == true) activeChannels.Add("P6");
        
        if (ChkN1?.IsChecked == true) activeChannels.Add("N1");
        if (ChkN2?.IsChecked == true) activeChannels.Add("N2");
        if (ChkB1?.IsChecked == true) activeChannels.Add("B1");
        
        if (ChkF1?.IsChecked == true) activeChannels.Add("F1");
        if (ChkF2?.IsChecked == true) activeChannels.Add("F2");
        if (ChkF3?.IsChecked == true) activeChannels.Add("F3");
        if (ChkF4?.IsChecked == true) activeChannels.Add("F4");
        if (ChkF5?.IsChecked == true) activeChannels.Add("F5");
        if (ChkF6?.IsChecked == true) activeChannels.Add("F6");
        if (ChkF7?.IsChecked == true) activeChannels.Add("F7");
        if (ChkF8?.IsChecked == true) activeChannels.Add("F8");
        
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

    // --- MML File Tree Handlers ---
    private void OpenMmlFolder_Click(object? sender, RoutedEventArgs e)
    {
        string path = System.IO.Path.GetFullPath(MmlLibrary.RootPath);
        if (!System.IO.Directory.Exists(path)) return;

        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer", path) { UseShellExecute = true });
        }
        else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
        {
            System.Diagnostics.Process.Start("open", path);
        }
        else
        {
            System.Diagnostics.Process.Start("xdg-open", path);
        }
    }

    private async void NewMmlFile_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("新規ファイル作成", "ファイル名を入力してください (*.mml):", "Untitled.mml");
        var result = await dialog.ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(result))
        {
            if (!result.EndsWith(".mml", System.StringComparison.OrdinalIgnoreCase)) result += ".mml";
            string newPath = System.IO.Path.Combine(MmlLibrary.RootPath, result);
            if (!System.IO.File.Exists(newPath))
            {
                System.IO.File.WriteAllText(newPath, "; New MML File\r\n");
                MmlLibrary.Refresh();
                OpenFileFromTree(newPath);
            }
        }
    }

    private void TreeView_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (e.Source is Control c && c.DataContext is FileTreeNodeViewModel node && !node.IsDirectory)
        {
            OpenFileFromTree(node.FullPath);
        }
    }

    private void TreeNode_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2 && sender is Control c && c.DataContext is FileTreeNodeViewModel node && !node.IsDirectory)
        {
            OpenFileFromTree(node.FullPath);
            e.Handled = true;
        }
    }

    private async void ContextNewFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is FileTreeNodeViewModel node)
        {
            string parentPath = node.IsDirectory ? node.FullPath : System.IO.Path.GetDirectoryName(node.FullPath) ?? MmlLibrary.RootPath;
            
            var dialog = new InputDialog("新規フォルダ作成", "フォルダ名を入力してください:");
            var result = await dialog.ShowDialog<string?>(this);
            if (!string.IsNullOrWhiteSpace(result))
            {
                string newPath = System.IO.Path.Combine(parentPath, result);
                if (!System.IO.Directory.Exists(newPath))
                {
                    System.IO.Directory.CreateDirectory(newPath);
                    MmlLibrary.Refresh();
                }
            }
        }
    }

    private async void ContextNewFile_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is FileTreeNodeViewModel node)
        {
            string parentPath = node.IsDirectory ? node.FullPath : System.IO.Path.GetDirectoryName(node.FullPath) ?? MmlLibrary.RootPath;
            
            var dialog = new InputDialog("新規ファイル作成", "ファイル名を入力してください (*.mml):", "Untitled.mml");
            var result = await dialog.ShowDialog<string?>(this);
            if (!string.IsNullOrWhiteSpace(result))
            {
                if (!result.EndsWith(".mml", System.StringComparison.OrdinalIgnoreCase)) result += ".mml";
                string newPath = System.IO.Path.Combine(parentPath, result);
                if (!System.IO.File.Exists(newPath))
                {
                    System.IO.File.WriteAllText(newPath, "; New MML File\r\n");
                    MmlLibrary.Refresh();
                    OpenFileFromTree(newPath);
                }
            }
        }
    }

    private async void ContextRename_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is FileTreeNodeViewModel node)
        {
            var dialog = new InputDialog("名前の変更", "新しい名前を入力してください:", node.Name);
            var result = await dialog.ShowDialog<string?>(this);
            if (!string.IsNullOrWhiteSpace(result) && result != node.Name)
            {
                string parentPath = System.IO.Path.GetDirectoryName(node.FullPath) ?? MmlLibrary.RootPath;
                string newPath = System.IO.Path.Combine(parentPath, result);
                
                try
                {
                    if (node.IsDirectory)
                    {
                        System.IO.Directory.Move(node.FullPath, newPath);
                    }
                    else
                    {
                        System.IO.File.Move(node.FullPath, newPath);
                    }
                    MmlLibrary.Refresh();
                }
                catch (System.Exception ex)
                {
                    LogTextBox.Text = $"Rename failed: {ex.Message}";
                }
            }
        }
    }

    private async void ContextDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is FileTreeNodeViewModel node)
        {
            var dialog = new ConfirmDialog("削除の確認", $"本当に '{node.Name}' を削除しますか？");
            var result = await dialog.ShowDialog<bool>(this);
            
            if (result == true)
            {
                try
                {
                    if (node.IsDirectory)
                    {
                        System.IO.Directory.Delete(node.FullPath, true);
                    }
                    else
                    {
                        System.IO.File.Delete(node.FullPath);
                    }
                    MmlLibrary.Refresh();
                }
                catch (System.Exception ex)
                {
                    LogTextBox.Text = $"Delete failed: {ex.Message}";
                }
            }
        }
    }

}

public class VolumeToWidthConverter : Avalonia.Data.Converters.IValueConverter
{
    public object? Convert(object? value, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is int vol)
        {
            if (vol > 15) return System.Math.Min(100, (vol / 127.0) * 100);
            return System.Math.Min(100, (vol / 15.0) * 100);
        }
        return 0.0;
    }

    public object? ConvertBack(object? value, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture) => null;
}
