using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Mz1500SoundPlayer.Sound;
using NAudio.Wave;
using System;
using System.Collections.Generic;

namespace Mz1500SoundPlayer;

public partial class VirtualKeyboardWindow : Window
{
    private ChannelState _state = new ChannelState("F1", 4, 15, -1, -1, 0, 3, 127, 0, 0);
    private MmlData? _mmlData;
    private int _baseOctave = 4;
    private int _currentOctave = 4;

    private readonly List<KeyInfo> _keys = new();
    public Action<string>? OnInsertMml { get; set; }

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
        public Control Control { get; set; } = null!;
        public string MmlName { get; set; } = "";
    }

    public VirtualKeyboardWindow()
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

    private void UpdateStateDisplay()
    {
        StateInfoText.Text = $"トラック: {_state.TrackName} | 音量 v{_state.Volume} | 音色 @{_state.FmVoiceId} | エンベロープ @v{_state.EnvelopeId}";
        OctaveDisplayText.Text = $"o{_currentOctave}";
    }

    private void InitAudio()
    {
        try
        {
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

        double whiteKeyWidth = 720.0 / 21.0; // 21 white keys (3 octaves * 7)
        double blackKeyWidth = whiteKeyWidth * 0.6;
        double whiteKeyHeight = 145.0;
        double blackKeyHeight = 90.0;

        string[] whiteNoteNames = { "c", "d", "e", "f", "g", "a", "b" };
        int[] whiteNoteOffsets = { 0, 2, 4, 5, 7, 9, 11 };

        // 1. Build White Keys
        int whiteCount = 0;
        for (int oct = 0; oct < 3; oct++)
        {
            for (int i = 0; i < 7; i++)
            {
                double left = whiteCount * whiteKeyWidth;
                var border = new Border
                {
                    Width = whiteKeyWidth - 2,
                    Height = whiteKeyHeight,
                    Background = Brushes.White,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Avalonia.Thickness(1),
                    CornerRadius = new Avalonia.CornerRadius(0, 0, 4, 4),
                    Tag = new KeyInfo
                    {
                        NoteName = whiteNoteNames[i],
                        OctaveOffset = oct,
                        NoteInOctave = whiteNoteOffsets[i],
                        IsBlack = false,
                        MmlName = whiteNoteNames[i]
                    }
                };

                Canvas.SetLeft(border, left);
                Canvas.SetTop(border, 0);

                var keyInfo = (KeyInfo)border.Tag;
                keyInfo.Control = border;
                _keys.Add(keyInfo);

                border.PointerPressed += Key_PointerPressed;
                border.PointerReleased += Key_PointerReleased;
                border.PointerExited += Key_PointerReleased;

                PianoCanvas.Children.Add(border);
                whiteCount++;
            }
        }

        // 2. Build Black Keys
        int[] blackNoteOffsets = { 1, 3, 6, 8, 10 };
        string[] blackMmlNames = { "c+", "d+", "f+", "g+", "a+" };
        int[] blackPositionsAfterWhite = { 0, 1, 3, 4, 5 }; // Index of white key after which black key goes

        for (int oct = 0; oct < 3; oct++)
        {
            for (int i = 0; i < 5; i++)
            {
                int whiteIdx = oct * 7 + blackPositionsAfterWhite[i];
                double left = (whiteIdx + 1) * whiteKeyWidth - (blackKeyWidth / 2.0);

                var border = new Border
                {
                    Width = blackKeyWidth,
                    Height = blackKeyHeight,
                    Background = Brushes.Black,
                    BorderBrush = Brushes.DarkGray,
                    BorderThickness = new Avalonia.Thickness(1),
                    CornerRadius = new Avalonia.CornerRadius(0, 0, 3, 3),
                    ZIndex = 10,
                    Tag = new KeyInfo
                    {
                        NoteName = blackMmlNames[i],
                        OctaveOffset = oct,
                        NoteInOctave = blackNoteOffsets[i],
                        IsBlack = true,
                        MmlName = blackMmlNames[i]
                    }
                };

                Canvas.SetLeft(border, left);
                Canvas.SetTop(border, 0);

                var keyInfo = (KeyInfo)border.Tag;
                keyInfo.Control = border;
                _keys.Add(keyInfo);

                border.PointerPressed += Key_PointerPressed;
                border.PointerReleased += Key_PointerReleased;
                border.PointerExited += Key_PointerReleased;

                PianoCanvas.Children.Add(border);
            }
        }
    }

    private void Key_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border b && b.Tag is KeyInfo key)
        {
            b.Background = key.IsBlack ? Brushes.DeepSkyBlue : Brushes.LightSkyBlue;
            PlayNote(key);
        }
    }

    private void Key_PointerReleased(object? sender, PointerEventArgs e)
    {
        if (sender is Border b && b.Tag is KeyInfo key)
        {
            b.Background = key.IsBlack ? Brushes.Black : Brushes.White;
            StopNote();
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
        if (_currentOctave > 1)
        {
            _currentOctave--;
            UpdateStateDisplay();
        }
    }

    private void OctaveUp_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentOctave < 7)
        {
            _currentOctave++;
            UpdateStateDisplay();
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

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        try
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
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
    private bool _isPlaying = false;
    private byte _fmChannel = 0;

    public SingleNoteProvider(YM2151Manager ym2151)
    {
        _ym2151 = ym2151;
        _intBuffer = new int[2][] { new int[44100], new int[44100] };
    }

    public void StartNote(double freq, ChannelState state, MmlData? mmlData)
    {
        bool isFm = state.TrackName.ToUpperInvariant().StartsWith("F") && state.TrackName.Length == 2;
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
            _ym2151.OutPort(0x0709, (byte)(0x78 | _fmChannel));
            
            _isPlaying = true;
        }
    }

    public void StopNote(ChannelState state)
    {
        bool isFm = state.TrackName.ToUpperInvariant().StartsWith("F") && state.TrackName.Length == 2;
        if (isFm)
        {
            // Key OFF
            _ym2151.OutPort(0x0708, 0x08);
            _ym2151.OutPort(0x0709, (byte)(0x00 | _fmChannel));
        }
        _isPlaying = false;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        if (_isPlaying)
        {
            Array.Clear(_intBuffer[0], 0, count);
            Array.Clear(_intBuffer[1], 0, count);
            _ym2151.GenerateSamples(_intBuffer, count);

            const float ym2151VolumeScale = 1.0f / 32768.0f;
            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] = (_intBuffer[0][i] + _intBuffer[1][i]) * 0.5f * ym2151VolumeScale;
            }
        }
        return count;
    }
}
