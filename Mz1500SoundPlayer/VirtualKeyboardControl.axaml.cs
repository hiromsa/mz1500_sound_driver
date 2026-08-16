using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Mz1500SoundPlayer.Sound;
using NAudio.Wave;
using System;
using System.Collections.Generic;

namespace Mz1500SoundPlayer;

public partial class VirtualKeyboardControl : UserControl
{
    private ChannelState _state = new ChannelState("F1", 4, 15, -1, -1, 0, 3, 127, 0, 0);
    private MmlData? _mmlData;
    private int _baseOctave = 4;
    private int _currentOctave = 4;

    private readonly List<KeyInfo> _keys = new();
    private readonly Dictionary<Key, KeyInfo> _physicalKeyMap = new();
    private readonly HashSet<Key> _pressedPhysicalKeys = new();

    private bool _isMouseDown = false;
    private KeyInfo? _activeMouseKey = null;

    public Action<string>? OnInsertMml { get; set; }

    public bool IsEditorMode
    {
        get => !StateInfoBorder.IsVisible;
        set
        {
            StateInfoBorder.IsVisible = !value;
            MmlRecordingGrid.IsVisible = !value;
        }
    }

    private SingleNoteProvider? _noteProvider;

    private class KeyInfo
    {
        public string NoteName { get; set; } = "";
        public int OctaveOffset { get; set; } // 0, 1, 2
        public int NoteInOctave { get; set; } // 0..11
        public bool IsBlack { get; set; }
        public Border Control { get; set; } = null!;
        public TextBlock LabelBlock { get; set; } = null!;
        public string MmlName { get; set; } = "";
        public string PhysicalChar { get; set; } = "";
        public int AbsoluteOctave { get; set; }
        public int IsWhiteIndex { get; set; }
        public int IsBlackIndex { get; set; }
    }

    public VirtualKeyboardControl()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            topLevel.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, OnGlobalKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            topLevel.AddHandler(Avalonia.Input.InputElement.KeyUpEvent, OnGlobalKeyUp, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            topLevel.RemoveHandler(Avalonia.Input.InputElement.KeyDownEvent, OnGlobalKeyDown);
            topLevel.RemoveHandler(Avalonia.Input.InputElement.KeyUpEvent, OnGlobalKeyUp);
        }
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Left)
        {
            if (KeyboardScrollViewer != null)
            {
                KeyboardScrollViewer.Offset = new Avalonia.Vector(Math.Max(0, KeyboardScrollViewer.Offset.X - 50), KeyboardScrollViewer.Offset.Y);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Right)
        {
            if (KeyboardScrollViewer != null)
            {
                KeyboardScrollViewer.Offset = new Avalonia.Vector(KeyboardScrollViewer.Offset.X + 50, KeyboardScrollViewer.Offset.Y);
                e.Handled = true;
            }
        }
        else
        {
            if (_physicalKeyMap.TryGetValue(e.Key, out var keyInfo) && !_pressedPhysicalKeys.Contains(e.Key))
            {
                _pressedPhysicalKeys.Add(e.Key);
                keyInfo.Control.Background = keyInfo.IsBlack ? Brushes.DeepSkyBlue : Brushes.LightSkyBlue;
                PlayNote(keyInfo);
                e.Handled = true;
            }
        }
    }

    private void OnGlobalKeyUp(object? sender, KeyEventArgs e)
    {
        if (_physicalKeyMap.TryGetValue(e.Key, out var keyInfo))
        {
            _pressedPhysicalKeys.Remove(e.Key);
            keyInfo.Control.Background = keyInfo.IsBlack ? Brushes.Black : Brushes.White;
            StopNote();
            e.Handled = true;
        }
    }

    public void InitializeState(ChannelState state, MmlData? mmlData)
    {
        _state = state;
        _mmlData = mmlData;
        _baseOctave = state.Octave;
        _currentOctave = state.Octave;

        UpdateStateDisplay();
        InitAudio();
        BuildKeyboard();
    }

    public void UpdateState(ChannelState state, MmlData? mmlData)
    {
        _state = state;
        _mmlData = mmlData;
        UpdateStateDisplay();
    }

    private void UpdateStateDisplay()
    {
        StateInfoText.Text = $"トラック: {_state.TrackName} | 音量 v{_state.Volume} | 音色 @{_state.FmVoiceId} | エンベロープ @v{_state.EnvelopeId}";
    }

    private void InitAudio()
    {
        SharedAudioEngine.Acquire();
        _noteProvider = SharedAudioEngine.NoteProvider;
    }

    public void DisposeAudio()
    {
        SharedAudioEngine.Release();
    }

    private void BuildKeyboard()
    {
        PianoCanvas.Children.Clear();
        _keys.Clear();

        int numOctaves = 8;
        int startOctave = 1;
        double whiteKeyWidth = 24.0;
        double blackKeyWidth = whiteKeyWidth * 0.6;
        double whiteKeyHeight = 80.0;
        double blackKeyHeight = 50.0;
        
        PianoCanvas.Width = numOctaves * 7 * whiteKeyWidth;

        string[] whiteNoteNames = { "c", "d", "e", "f", "g", "a", "b" };
        int[] whiteNoteOffsets = { 0, 2, 4, 5, 7, 9, 11 };

        // 1. Build White Keys
        int whiteCount = 0;
        for (int oct = 0; oct < numOctaves; oct++)
        {
            int absOct = startOctave + oct;

            for (int i = 0; i < 7; i++)
            {
                double left = whiteCount * whiteKeyWidth;

                var textBlock = new TextBlock
                {
                    Text = (i == 0) ? $"o{absOct}" : "",
                    FontSize = (i == 0) ? 10 : 11,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Avalonia.Thickness(0, 0, 0, 6)
                };

                var grid = new Grid();
                grid.Children.Add(textBlock);

                var border = new Border
                {
                    Width = whiteKeyWidth - 2,
                    Height = whiteKeyHeight,
                    Background = Brushes.White,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Avalonia.Thickness(1),
                    CornerRadius = new Avalonia.CornerRadius(0, 0, 4, 4),
                    Child = grid,
                    Tag = new KeyInfo
                    {
                        NoteName = whiteNoteNames[i],
                        OctaveOffset = 0, // will be computed in PlayNote
                        NoteInOctave = whiteNoteOffsets[i],
                        IsBlack = false,
                        MmlName = whiteNoteNames[i],
                        AbsoluteOctave = absOct,
                        IsWhiteIndex = i
                    }
                };

                Canvas.SetLeft(border, left);
                Canvas.SetTop(border, 0);

                var keyInfo = (KeyInfo)border.Tag;
                keyInfo.Control = border;
                keyInfo.LabelBlock = textBlock;
                _keys.Add(keyInfo);

                border.PointerPressed += Key_PointerPressed;
                border.PointerEntered += Key_PointerEntered;
                border.PointerReleased += Key_PointerReleased;

                PianoCanvas.Children.Add(border);
                whiteCount++;
            }
        }

        // 2. Build Black Keys
        int[] blackNoteOffsets = { 1, 3, 6, 8, 10 };
        string[] blackMmlNames = { "c+", "d+", "f+", "g+", "a+" };
        int[] blackPositionsAfterWhite = { 0, 1, 3, 4, 5 };

        int blackCount = 0;
        for (int oct = 0; oct < numOctaves; oct++)
        {
            int absOct = startOctave + oct;

            for (int i = 0; i < 5; i++)
            {
                int whiteIdx = oct * 7 + blackPositionsAfterWhite[i];
                double left = (whiteIdx + 1) * whiteKeyWidth - (blackKeyWidth / 2.0);

                var textBlock = new TextBlock
                {
                    Text = "",
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.LightGray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Avalonia.Thickness(0, 0, 0, 4)
                };

                var grid = new Grid();
                grid.Children.Add(textBlock);

                var border = new Border
                {
                    Width = blackKeyWidth,
                    Height = blackKeyHeight,
                    Background = Brushes.Black,
                    BorderBrush = Brushes.DarkGray,
                    BorderThickness = new Avalonia.Thickness(1),
                    CornerRadius = new Avalonia.CornerRadius(0, 0, 3, 3),
                    ZIndex = 10,
                    Child = grid,
                    Tag = new KeyInfo
                    {
                        NoteName = blackMmlNames[i],
                        OctaveOffset = 0,
                        NoteInOctave = blackNoteOffsets[i],
                        IsBlack = true,
                        MmlName = blackMmlNames[i],
                        AbsoluteOctave = absOct,
                        IsBlackIndex = i
                    }
                };

                Canvas.SetLeft(border, left);
                Canvas.SetTop(border, 0);

                var keyInfo = (KeyInfo)border.Tag;
                keyInfo.Control = border;
                keyInfo.LabelBlock = textBlock;
                _keys.Add(keyInfo);

                border.PointerPressed += Key_PointerPressed;
                border.PointerEntered += Key_PointerEntered;
                border.PointerReleased += Key_PointerReleased;

                PianoCanvas.Children.Add(border);
                blackCount++;
            }
        }
        
        UpdateKeyAssignments();
    }

    private void UpdateKeyAssignments()
    {
        _physicalKeyMap.Clear();

        Key[] whitePhysicalKeys = {
            Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M,
            Key.OemComma, Key.OemPeriod, Key.OemQuestion
        };

        string[] whitePhysicalChars = {
            "Z", "X", "C", "V", "B", "N", "M",
            ",", ".", "/"
        };

        Key[] blackPhysicalKeys = {
            Key.S, Key.D, Key.G, Key.H, Key.J,
            Key.L, Key.OemSemicolon
        };

        string[] blackPhysicalChars = {
            "S", "D", "G", "H", "J",
            "L", ";"
        };

        foreach (var key in _keys)
        {
            key.PhysicalChar = "";
            
            if (!key.IsBlack)
            {
                int octOffset = key.AbsoluteOctave - _currentOctave;
                if (octOffset >= 0 && octOffset < 2)
                {
                    int physIndex = octOffset * 7 + key.IsWhiteIndex;
                    if (physIndex < whitePhysicalChars.Length)
                    {
                        key.PhysicalChar = whitePhysicalChars[physIndex];
                        _physicalKeyMap[whitePhysicalKeys[physIndex]] = key;
                    }
                }
                
                string label = key.PhysicalChar;
                if (key.IsWhiteIndex == 0) label = $"o{key.AbsoluteOctave}\n{label}";
                key.LabelBlock.Text = label;
            }
            else
            {
                int octOffset = key.AbsoluteOctave - _currentOctave;
                if (octOffset >= 0 && octOffset < 2)
                {
                    int physIndex = octOffset * 5 + key.IsBlackIndex;
                    if (physIndex < blackPhysicalChars.Length)
                    {
                        key.PhysicalChar = blackPhysicalChars[physIndex];
                        _physicalKeyMap[blackPhysicalKeys[physIndex]] = key;
                    }
                }
                
                key.LabelBlock.Text = key.PhysicalChar;
            }
        }
    }

    private void Key_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border b && b.Tag is KeyInfo key)
        {
            _isMouseDown = true;
            e.Pointer.Capture(b);
            SwitchToKey(key);
        }
    }

    private void Key_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (_isMouseDown && sender is Border b && b.Tag is KeyInfo key && key != _activeMouseKey)
        {
            SwitchToKey(key);
        }
    }

    private void Key_PointerReleased(object? sender, PointerEventArgs e)
    {
        _isMouseDown = false;
        e.Pointer.Capture(null);
        if (_activeMouseKey != null)
        {
            _activeMouseKey.Control.Background = _activeMouseKey.IsBlack ? Brushes.Black : Brushes.White;
            _activeMouseKey = null;
            StopNote();
        }
    }

    private void SwitchToKey(KeyInfo key)
    {
        int actualOctave = key.AbsoluteOctave;
        if (actualOctave < 1 || actualOctave > 8) return;

        if (_activeMouseKey != null)
        {
            _activeMouseKey.Control.Background = _activeMouseKey.IsBlack ? Brushes.Black : Brushes.White;
        }

        _activeMouseKey = key;
        key.Control.Background = key.IsBlack ? Brushes.DeepSkyBlue : Brushes.LightSkyBlue;
        PlayNote(key);
    }

    // Removed redundant OnKeyDown and OnKeyUp since they are now handled globally.

    private int _lastPlayedOctave = -1;

    private void PlayNote(KeyInfo key)
    {
        int actualOctave = key.AbsoluteOctave;
        int noteIndex = actualOctave * 12 + key.NoteInOctave;
        double freq = 440.0 * Math.Pow(2.0, (noteIndex - 57) / 12.0);

        // Record MML if enabled
        if (RecordCheckBox.IsChecked == true)
        {
            string mmlNote = "";
            if (_lastPlayedOctave == -1)
            {
                _lastPlayedOctave = actualOctave;
                mmlNote = $"o{actualOctave}{key.MmlName}";
            }
            else if (actualOctave > _lastPlayedOctave)
            {
                int diff = actualOctave - _lastPlayedOctave;
                _lastPlayedOctave = actualOctave;
                mmlNote = new string('>', diff) + key.MmlName;
            }
            else if (actualOctave < _lastPlayedOctave)
            {
                int diff = _lastPlayedOctave - actualOctave;
                _lastPlayedOctave = actualOctave;
                mmlNote = new string('<', diff) + key.MmlName;
            }
            else
            {
                mmlNote = key.MmlName;
            }

            if (string.IsNullOrEmpty(RecordTextBox.Text))
                RecordTextBox.Text = mmlNote;
            else
                RecordTextBox.Text += " " + mmlNote;
        }

        // Trigger Audio KeyOn
        _noteProvider?.StartNote(freq, _state, _mmlData);
    }

    private void StopNote()
    {
        _noteProvider?.StopNote(_state);
    }

    private void OctaveDown_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentOctave > 1)
        {
            _currentOctave--;
            UpdateKeyAssignments();
        }
    }

    private void OctaveUp_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentOctave < 6)
        {
            _currentOctave++;
            UpdateKeyAssignments();
        }
    }

    private void ClearRecord_Click(object? sender, RoutedEventArgs e)
    {
        RecordTextBox.Text = "";
        _lastPlayedOctave = -1;
    }

    private void InsertToEditor_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(RecordTextBox.Text))
        {
            OnInsertMml?.Invoke(RecordTextBox.Text);
        }
    }

}

/// <summary>
/// Helper provider for real-time single note synthesis via YM2151 or PSG
/// </summary>
public class SingleNoteProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

    private readonly YM2151Manager _ym2151;
    private readonly int[][] _intBuffer;

    private bool _isFm = true;
    private byte _fmChannel = 0;

    // PSG synthesis parameters
    private double _psgPhase = 0;
    private double _psgFreq = 0;
    private float _psgVolume = 0f;
    private bool _psgActive = false;
    private float _psgEnvelope = 0f;

    public SingleNoteProvider(YM2151Manager ym2151)
    {
        _ym2151 = ym2151;
        _intBuffer = new int[2][] { new int[44100], new int[44100] };
    }

    public void StartNote(double freq, ChannelState state, MmlData? mmlData)
    {
        bool isFm = state.TrackName.ToUpperInvariant().StartsWith("F") && state.TrackName.Length == 2;
        _isFm = isFm;

        if (isFm)
        {
            _fmChannel = (byte)(int.Parse(state.TrackName.Substring(1)) - 1);
            
            // Set instrument if available
            if (mmlData != null && mmlData.FmVoiceEnvelopes.TryGetValue(state.FmVoiceId, out var toneData))
            {
                int[] p = toneData.Parameters;
                if (p.Length >= 46)
                {
                    byte panFlCon = (byte)(((state.FmPan & 3) << 6) | ((p[1] & 7) << 3) | (p[0] & 7));
                    _ym2151.OutPort(0x0708, (byte)(0x20 + _fmChannel));
                    _ym2151.OutPort(0x0709, panFlCon);
                    
                    for (int op = 0; op < 4; op++)
                    {
                        int slotNum = op;
                        if (op == 1) slotNum = 2; // OP2 -> Slot 3 (C1)
                        else if (op == 2) slotNum = 1; // OP3 -> Slot 2 (M2)

                        int opOffset = slotNum * 8;
                        int pd = 2 + (op * 11);
                        _ym2151.OutPort(0x0708, (byte)(0x40 + opOffset + _fmChannel));
                        _ym2151.OutPort(0x0709, (byte)(((p[pd+8]&7)<<4) | (p[pd+7]&15)));
                        _ym2151.OutPort(0x0708, (byte)(0x60 + opOffset + _fmChannel));
                        _ym2151.OutPort(0x0709, (byte)(p[pd+5] & 127));
                        _ym2151.OutPort(0x0708, (byte)(0x80 + opOffset + _fmChannel));
                        _ym2151.OutPort(0x0709, (byte)(((p[pd+6]&3)<<6) | (p[pd+0]&31)));
                        _ym2151.OutPort(0x0708, (byte)(0xA0 + opOffset + _fmChannel));
                        _ym2151.OutPort(0x0709, (byte)(((p[pd+10]&1)<<7) | (p[pd+1]&31)));
                        _ym2151.OutPort(0x0708, (byte)(0xC0 + opOffset + _fmChannel));
                        _ym2151.OutPort(0x0709, (byte)(((p[pd+9]&3)<<6) | (p[pd+2]&31)));
                        _ym2151.OutPort(0x0708, (byte)(0xE0 + opOffset + _fmChannel));
                        _ym2151.OutPort(0x0709, (byte)(((p[pd+4]&15)<<4) | (p[pd+3]&15)));
                    }
                }
            }

            // Key ON
            Ym2151Helper.GetKcKf(freq, out byte kc, out byte kf);
            _ym2151.OutPort(0x0708, (byte)(0x28 + _fmChannel));
            _ym2151.OutPort(0x0709, kc);
            _ym2151.OutPort(0x0708, (byte)(0x30 + _fmChannel));
            _ym2151.OutPort(0x0709, kf);
            _ym2151.OutPort(0x0708, 0x08);
            
            byte keyOnMask = 0x78;
            if (mmlData != null && mmlData.FmVoiceEnvelopes.TryGetValue(state.FmVoiceId, out var td))
            {
                keyOnMask = td.KeyOnMask;
            }
            _ym2151.OutPort(0x0709, (byte)(keyOnMask | _fmChannel));
        }
        else
        {
            // PSG Single Note synthesis
            _psgFreq = freq;
            _psgVolume = (Math.Max(1, state.Volume) / 15.0f) * 0.25f;
            _psgActive = true;
            _psgEnvelope = 1.0f;
        }
    }

    public void StopNote(ChannelState state)
    {
        bool isFm = state.TrackName.ToUpperInvariant().StartsWith("F") && state.TrackName.Length == 2;
        if (isFm)
        {
            // Send Key OFF to YM2151
            _ym2151.OutPort(0x0708, 0x08);
            _ym2151.OutPort(0x0709, (byte)(0x00 | _fmChannel));
        }
        else
        {
            _psgActive = false;
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);

        if (_isFm)
        {
            if (_intBuffer[0].Length < count)
            {
                _intBuffer[0] = new int[count];
                _intBuffer[1] = new int[count];
            }

            Array.Clear(_intBuffer[0], 0, count);
            Array.Clear(_intBuffer[1], 0, count);
            _ym2151.GenerateSamples(_intBuffer, count);

            const float ym2151VolumeScale = 1.0f / 32768.0f;
            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] = (_intBuffer[0][i] + _intBuffer[1][i]) * 0.5f * ym2151VolumeScale;
            }
        }
        else if (!_isFm && _psgFreq > 0)
        {
            // Square wave synthesis for PSG
            double phaseInc = _psgFreq / WaveFormat.SampleRate;
            for (int i = 0; i < count; i++)
            {
                if (_psgActive)
                {
                    _psgEnvelope = 1.0f;
                }
                else
                {
                    _psgEnvelope *= 0.999f; // fast release
                }
                
                if (_psgEnvelope > 0.001f)
                {
                    float currentVol = _psgVolume * _psgEnvelope;
                    buffer[offset + i] = _psgPhase < 0.5 ? currentVol : -currentVol;
                    _psgPhase += phaseInc;
                    if (_psgPhase >= 1.0) _psgPhase -= 1.0;
                }
            }
        }

        return count;
    }
}
