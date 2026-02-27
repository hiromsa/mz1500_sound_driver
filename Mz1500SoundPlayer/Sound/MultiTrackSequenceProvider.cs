using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace Mz1500SoundPlayer.Sound;

public class MultiTrackSequenceProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; }
    public HashSet<string> ActiveChannels { get; set; } = new HashSet<string>(new[] { "A", "B", "C", "D", "E", "F", "G", "H", "P" });

    // トラック毎の独立したシーケンスプロバイダを保持
    private readonly List<(string TrackName, MmlSequenceProvider Provider)> _trackProviders;
    private float[]? _tempBuffer;

    public MultiTrackSequenceProvider(Dictionary<string, byte[]> trackBinaries, Dictionary<int, EnvelopeData> envelopes, List<MmlToZ80Compiler.HwPitchEnvData> hwPitchEnvelopes, int sampleRate = 44100)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _trackProviders = new List<(string TrackName, MmlSequenceProvider Provider)>();

        Console.WriteLine($"[MultiTrackSequenceProvider] Init with {trackBinaries.Count} tracks.");

        foreach (var kvp in trackBinaries)
        {
            if (kvp.Value.Length > 0)
            {
                bool isBeep = kvp.Key.ToUpperInvariant() == "P";
                Console.WriteLine($"[MultiTrackSequenceProvider] Track {kvp.Key} has {kvp.Value.Length} bytes.");
                _trackProviders.Add((kvp.Key.ToUpperInvariant(), new MmlSequenceProvider(kvp.Value, envelopes, hwPitchEnvelopes, sampleRate, isBeep)));
            }
        }
    }

    public Dictionary<string, int> GetCurrentVolumes()
    {
        var vols = new Dictionary<string, int>();
        foreach (var item in _trackProviders)
        {
            vols[item.TrackName] = item.Provider.CurrentVolume;
        }
        return vols;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (_tempBuffer == null || _tempBuffer.Length < count)
        {
            _tempBuffer = new float[count];
        }
        
        // 最終出力バッファをゼロクリア
        Array.Clear(buffer, offset, count);

        foreach (var item in _trackProviders)
        {
            var provider = item.Provider;
            provider.IsMuted = !ActiveChannels.Contains(item.TrackName);

            int read = provider.Read(_tempBuffer, 0, count);
            if (read > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    buffer[offset + i] += _tempBuffer[i];
                }
            }
        }

        // オーバーフロー(クリッピング)の簡易防止 (本来はリミッター等が望ましい)
        for (int i = 0; i < count; i++)
        {
            if (buffer[offset + i] > 1.0f) buffer[offset + i] = 1.0f;
            else if (buffer[offset + i] < -1.0f) buffer[offset + i] = -1.0f;
        }

        // if (!hasMoreData) return 0; とすると再生が終了するが、
        // NAudio側で突然切れるのを防ぐため無音を返し続けるか適宜判断する
        return count;
    }
}
