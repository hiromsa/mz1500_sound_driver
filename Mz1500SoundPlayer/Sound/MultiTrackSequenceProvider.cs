using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace Mz1500SoundPlayer.Sound;

public class MultiTrackSequenceProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; }

    // トラック毎の独立したシーケンスプロバイダを保持
    private readonly List<MmlSequenceProvider> _trackProviders;
    private float[]? _tempBuffer;

    public MultiTrackSequenceProvider(Dictionary<string, byte[]> trackBinaries, Dictionary<int, EnvelopeData> envelopes, List<MmlToZ80Compiler.HwPitchEnvData> hwPitchEnvelopes, int sampleRate = 44100)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _trackProviders = new List<MmlSequenceProvider>();

        Console.WriteLine($"[MultiTrackSequenceProvider] Init with {trackBinaries.Count} tracks.");

        foreach (var kvp in trackBinaries)
        {
            if (kvp.Value.Length > 0)
            {
                Console.WriteLine($"[MultiTrackSequenceProvider] Track {kvp.Key} has {kvp.Value.Length} bytes.");
                _trackProviders.Add(new MmlSequenceProvider(kvp.Value, envelopes, hwPitchEnvelopes, sampleRate));
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (_tempBuffer == null || _tempBuffer.Length < count)
        {
            _tempBuffer = new float[count];
        }
        
        // 最終出力バッファをゼロクリア
        Array.Clear(buffer, offset, count);

        foreach (var provider in _trackProviders)
        {
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
