using NAudio.Wave;
using System;
using Mz1500SoundPlayer.Sound;

namespace Mz1500SoundPlayer;

public static class SharedAudioEngine
{
    private static WasapiOut? _waveOut;
    private static YM2151Manager? _ym2151Manager;
    private static SingleNoteProvider? _noteProvider;
    private static int _refCount = 0;

    public static SingleNoteProvider? NoteProvider => _noteProvider;

    public static void Acquire()
    {
        if (_refCount == 0)
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
                Console.WriteLine($"Failed to init shared audio: {ex.Message}");
            }
        }
        _refCount++;
    }

    public static void Release()
    {
        _refCount--;
        if (_refCount <= 0)
        {
            _refCount = 0;
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
            _noteProvider = null;
            _ym2151Manager = null;
        }
    }
}
