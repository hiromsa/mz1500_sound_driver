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

public partial class VirtualKeyboardWindow : Window
{
    public VirtualKeyboardWindow()
    {
        InitializeComponent();
        KeyboardControl.OnInsertMml = (mml) => OnInsertMml?.Invoke(mml);
    }

    public Action<string>? OnInsertMml { get; set; }

    public void InitializeState(ChannelState state, MmlData? mmlData)
    {
        KeyboardControl.InitializeState(state, mmlData);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        KeyboardControl.DisposeAudio();
    }
}


