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
    private readonly ErrorHighlightRenderer _errorRenderer;
    private readonly DispatcherTimer _playbackTimer;
    private readonly DispatcherTimer _validationTimer;

    public MainWindow()
    {
        InitializeComponent();
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
        if (sender is Button btn)
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