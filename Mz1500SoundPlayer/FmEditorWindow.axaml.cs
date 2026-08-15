using Avalonia.Controls;
using Avalonia.Interactivity;
using Mz1500SoundPlayer.Sound;
using System;

namespace Mz1500SoundPlayer;

public partial class FmEditorWindow : Window
{
    public FmEditorViewModel ViewModel { get; }
    public Action<string>? OnApply { get; set; }
    
    private readonly int _fmNumber;

    public FmEditorWindow()
    {
        InitializeComponent();
        ViewModel = new FmEditorViewModel();
        DataContext = ViewModel;
        _fmNumber = 1;
    }

    public FmEditorWindow(int fmNumber, string mml)
    {
        InitializeComponent();
        ViewModel = new FmEditorViewModel();
        DataContext = ViewModel;
        _fmNumber = fmNumber;

        ViewModel.ParseMml(mml);
        
        // Initialize keyboard
        var state = new ChannelState($"F1", 4, 15, -1, fmNumber, 0, 3, 127, 0, 0);
        var mmlData = new MmlData();
        var td = new FmToneData { Parameters = ParseParameters(mml) };
        td.KeyOnMask = 0x78; // Start fully unmuted
        mmlData.FmVoiceEnvelopes[fmNumber] = td;
        KeyboardControl.InitializeState(state, mmlData);

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Op1.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Op2.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Op3.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Op4.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdateKeyboardState();
    }

    private void UpdateKeyboardState()
    {
        var state = new ChannelState($"F1", 4, 15, -1, _fmNumber, 0, 3, 127, 0, 0);
        var mmlData = new MmlData();
        var td = new FmToneData { Parameters = ParseParameters(ViewModel.ToMml(_fmNumber)) };
        
        byte mask = 0x78;
        if (ViewModel.Op1.IsMuted) mask &= unchecked((byte)~0x08);
        if (ViewModel.Op2.IsMuted) mask &= unchecked((byte)~0x10);
        if (ViewModel.Op3.IsMuted) mask &= unchecked((byte)~0x20);
        if (ViewModel.Op4.IsMuted) mask &= unchecked((byte)~0x40);

        bool hasSolo = ViewModel.Op1.IsSolo || ViewModel.Op2.IsSolo || ViewModel.Op3.IsSolo || ViewModel.Op4.IsSolo;
        if (hasSolo) {
            mask = 0;
            if (ViewModel.Op1.IsSolo) mask |= 0x08;
            if (ViewModel.Op2.IsSolo) mask |= 0x10;
            if (ViewModel.Op3.IsSolo) mask |= 0x20;
            if (ViewModel.Op4.IsSolo) mask |= 0x40;
        }

        td.KeyOnMask = mask;
        mmlData.FmVoiceEnvelopes[_fmNumber] = td;
        KeyboardControl.UpdateState(state, mmlData);
    }

    private int[] ParseParameters(string mml)
    {
        var match = System.Text.RegularExpressions.Regex.Match(mml, @"\{([^}]+)\}");
        if (match.Success)
        {
            var parts = match.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 46)
            {
                var ret = new int[46];
                for(int i = 0; i < 46; i++)
                    if (int.TryParse(parts[i].Trim(), out int val))
                        ret[i] = val;
                return ret;
            }
        }
        return new int[46];
    }

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        OnApply?.Invoke(ViewModel.ToMml(_fmNumber));
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        KeyboardControl.DisposeAudio();
    }
}
