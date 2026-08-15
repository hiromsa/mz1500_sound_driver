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

    // Audio output components
    private YM2151Manager? _ym2151Manager;
    private SingleNoteProvider? _noteProvider;
    private WasapiOut? _waveOut;

    private class KeyInfo
    {
        public string NoteName { get; set; } = "";
        public int OctaveOffset { get; set; } // 0, 1, 2
        public int NoteInOctave { get; set; } // 0..11
        public bool IsBlack { get; set; }
        public Border Control { get; set; } = null!;
        public string MmlName { get; set; } = "";
        public string PhysicalChar { get; set; } = "";
    }

    public VirtualKeyboardControl()
    {
        InitializeComponent();
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
        try
        {
            if (_waveOut != null) return;
            
            _ym2151Manager = new YM2151Manager(44100);
            _noteProvider = new SingleNoteProvider(_ym2151Manager);
            _waveOut = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 40);
            _waveOut.Init(_noteProvider);
            _waveOut.Play();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to init audio for VirtualKeyboard: {ex.Message}");
        }
    }

    private void BuildKeyboard()
    {
        PianoCanvas.Children.Clear();
        _keys.Clear();
        _physicalKeyMap.Clear();

        int numOctaves = 6;
        int startOctave = _currentOctave - 2;
        double whiteKeyWidth = 1000.0 / (numOctaves * 7);
        double blackKeyWidth = whiteKeyWidth * 0.6;
        double whiteKeyHeight = 80.0;
        double blackKeyHeight = 50.0;

        string[] whiteNoteNames = { "c", "d", "e", "f", "g", "a", "b" };
        int[] whiteNoteOffsets = { 0, 2, 4, 5, 7, 9, 11 };

        // Physical Keyboard Mappings
        Key[] whitePhysicalKeys = {
            Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M,
            Key.OemComma, Key.OemPeriod, Key.OemQuestion, Key.Q, Key.W, Key.E, Key.R,
            Key.T, Key.Y, Key.U, Key.I, Key.O, Key.P, Key.OemOpenBrackets
        };

        string[] whitePhysicalChars = {
            "Z", "X", "C", "V", "B", "N", "M",
            ",", ".", "/", "Q", "W", "E", "R",
            "T", "Y", "U", "I", "O", "P", "["
        };

        Key[] blackPhysicalKeys = {
            Key.S, Key.D, Key.G, Key.H, Key.J,
            Key.L, Key.OemSemicolon, Key.D2, Key.D3, Key.D5,
            Key.D6, Key.D7, Key.D9, Key.D0, Key.OemMinus
        };

        string[] blackPhysicalChars = {
            "S", "D", "G", "H", "J",
            "L", ";", "2", "3", "5",
            "6", "7", "9", "0", "-"
        };

        // 1. Build White Keys
        int whiteCount = 0;
        for (int oct = 0; oct < numOctaves; oct++)
        {
            int absOct = startOctave + oct;
            bool isValidOctave = absOct >= 0 && absOct <= 7;

            for (int i = 0; i < 7; i++)
            {
                double left = whiteCount * whiteKeyWidth;
                int physIndex = oct * 7 + i;

                string physChar = physIndex < whitePhysicalChars.Length ? whitePhysicalChars[physIndex] : "";
                Key physKey = physIndex < whitePhysicalKeys.Length ? whitePhysicalKeys[physIndex] : Key.None;

                string keyLabel = physChar;
                if (i == 0 && isValidOctave)
                {
                    keyLabel = $"o{absOct}\n{physChar}";
                }

                var textBlock = new TextBlock
                {
                    Text = keyLabel,
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
                    Background = isValidOctave ? Brushes.White : new SolidColorBrush(Color.Parse("#888888")),
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Avalonia.Thickness(1),
                    CornerRadius = new Avalonia.CornerRadius(0, 0, 4, 4),
                    Child = grid,
                    Tag = new KeyInfo
                    {
                        NoteName = whiteNoteNames[i],
                        OctaveOffset = oct - 2,
                        NoteInOctave = whiteNoteOffsets[i],
                        IsBlack = false,
                        MmlName = whiteNoteNames[i],
                        PhysicalChar = physChar
                    }
                };

                Canvas.SetLeft(border, left);
                Canvas.SetTop(border, 0);

                var keyInfo = (KeyInfo)border.Tag;
                keyInfo.Control = border;
                _keys.Add(keyInfo);

                if (physKey != Key.None)
                {
                    _physicalKeyMap[physKey] = keyInfo;
                }

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
            bool isValidOctave = absOct >= 0 && absOct <= 7;

            for (int i = 0; i < 5; i++)
            {
                int whiteIdx = oct * 7 + blackPositionsAfterWhite[i];
                double left = (whiteIdx + 1) * whiteKeyWidth - (blackKeyWidth / 2.0);
                int physIndex = blackCount;

                string physChar = physIndex < blackPhysicalChars.Length ? blackPhysicalChars[physIndex] : "";
                Key physKey = physIndex < blackPhysicalKeys.Length ? blackPhysicalKeys[physIndex] : Key.None;

                var textBlock = new TextBlock
                {
                    Text = physChar,
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
                    Background = isValidOctave ? Brushes.Black : new SolidColorBrush(Color.Parse("#444444")),
                    BorderBrush = Brushes.DarkGray,
                    BorderThickness = new Avalonia.Thickness(1),
                    CornerRadius = new Avalonia.CornerRadius(0, 0, 3, 3),
                    ZIndex = 10,
                    Child = grid,
                    Tag = new KeyInfo
                    {
                        NoteName = blackMmlNames[i],
                        OctaveOffset = oct - 2,
                        NoteInOctave = blackNoteOffsets[i],
                        IsBlack = true,
                        MmlName = blackMmlNames[i],
                        PhysicalChar = physChar
                    }
                };

                Canvas.SetLeft(border, left);
                Canvas.SetTop(border, 0);

                var keyInfo = (KeyInfo)border.Tag;
                keyInfo.Control = border;
                _keys.Add(keyInfo);

                if (physKey != Key.None)
                {
                    _physicalKeyMap[physKey] = keyInfo;
                }

                border.PointerPressed += Key_PointerPressed;
                border.PointerEntered += Key_PointerEntered;
                border.PointerReleased += Key_PointerReleased;

                PianoCanvas.Children.Add(border);
                blackCount++;
            }
        }
    }

    private void Key_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border b && b.Tag is KeyInfo key)
        {
            _isMouseDown = true;
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
        if (_activeMouseKey != null)
        {
            _activeMouseKey.Control.Background = _activeMouseKey.IsBlack ? Brushes.Black : Brushes.White;
            _activeMouseKey = null;
            StopNote();
        }
    }

    private void SwitchToKey(KeyInfo key)
    {
        int actualOctave = _currentOctave + key.OctaveOffset;
        if (actualOctave < 0 || actualOctave > 7) return;

        if (_activeMouseKey != null)
        {
            _activeMouseKey.Control.Background = _activeMouseKey.IsBlack ? Brushes.Black : Brushes.White;
        }

        _activeMouseKey = key;
        key.Control.Background = key.IsBlack ? Brushes.DeepSkyBlue : Brushes.LightSkyBlue;
        PlayNote(key);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_physicalKeyMap.TryGetValue(e.Key, out var keyInfo) && !_pressedPhysicalKeys.Contains(e.Key))
        {
            _pressedPhysicalKeys.Add(e.Key);
            keyInfo.Control.Background = keyInfo.IsBlack ? Brushes.DeepSkyBlue : Brushes.LightSkyBlue;
            PlayNote(keyInfo);
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (_physicalKeyMap.TryGetValue(e.Key, out var keyInfo))
        {
            _pressedPhysicalKeys.Remove(e.Key);
            keyInfo.Control.Background = keyInfo.IsBlack ? Brushes.Black : Brushes.White;
            StopNote();
            e.Handled = true;
        }
    }

    private int _lastPlayedOctave = -1;

    private void PlayNote(KeyInfo key)
    {
        int actualOctave = _currentOctave + key.OctaveOffset;
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
        if (_currentOctave > 0)
        {
            _currentOctave--;
            UpdateStateDisplay();
            BuildKeyboard();
        }
    }

    private void OctaveUp_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentOctave < 8)
        {
            _currentOctave++;
            UpdateStateDisplay();
            BuildKeyboard();
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

    public void DisposeAudio()
    {
        try
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
        }
        catch { }
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
                        int opOffset = op * 8;
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
        else if (_psgActive && _psgFreq > 0)
        {
            // Square wave synthesis for PSG
            double phaseInc = _psgFreq / WaveFormat.SampleRate;
            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] = _psgPhase < 0.5 ? _psgVolume : -_psgVolume;
                _psgPhase += phaseInc;
                if (_psgPhase >= 1.0) _psgPhase -= 1.0;
            }
        }

        return count;
    }
}
