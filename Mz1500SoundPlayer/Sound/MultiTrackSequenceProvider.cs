using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace Mz1500SoundPlayer.Sound;

public class MultiTrackSequenceProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; }
    public HashSet<string> ActiveChannels { get; set; } = new HashSet<string>(new[] { "A", "B", "C", "D", "E", "F", "G", "H", "P", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8" });

    // トラック毎の独立したシーケンスプロバイダを保持
    private readonly List<(string TrackName, ISampleProvider Provider)> _trackProviders;
    private float[]? _tempBuffer;
    private float[]? _ym2151Buffer;
    private int[][]? _ym2151IntBuffer;
    
    public YM2151Manager YM2151 { get; }

    public MultiTrackSequenceProvider(Dictionary<string, byte[]> trackBinaries, Dictionary<int, EnvelopeData> envelopes, List<MmlToZ80Compiler.HwPitchEnvData> hwPitchEnvelopes, int sampleRate = 44100)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _trackProviders = new List<(string TrackName, ISampleProvider Provider)>();
        YM2151 = new YM2151Manager(sampleRate);

        Console.WriteLine($"[MultiTrackSequenceProvider] Init with {trackBinaries.Count} tracks.");

        foreach (var kvp in trackBinaries)
        {
            if (kvp.Value.Length > 0)
            {
                bool isBeep = kvp.Key.ToUpperInvariant() == "P";
                bool isFm = kvp.Key.ToUpperInvariant().StartsWith("F") && kvp.Key.Length == 2;
                Console.WriteLine($"[MultiTrackSequenceProvider] Track {kvp.Key} has {kvp.Value.Length} bytes.");
                
                if (isFm)
                {
                    _trackProviders.Add((kvp.Key.ToUpperInvariant(), new Ym2151SequenceProvider(kvp.Value, YM2151, sampleRate)));
                }
                else
                {
                    _trackProviders.Add((kvp.Key.ToUpperInvariant(), new MmlSequenceProvider(kvp.Value, envelopes, hwPitchEnvelopes, sampleRate, isBeep)));
                }
            }
        }
    }

    public Dictionary<string, int> GetCurrentVolumes()
    {
        var vols = new Dictionary<string, int>();
        foreach (var item in _trackProviders)
        {
            if (item.Provider is MmlSequenceProvider m) vols[item.TrackName] = m.CurrentVolume;
            else vols[item.TrackName] = 0;
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
            bool isMuted = !ActiveChannels.Contains(item.TrackName);
            if (provider is MmlSequenceProvider m) m.IsMuted = isMuted;
            if (provider is Ym2151SequenceProvider y) y.IsMuted = isMuted;

            int read = provider.Read(_tempBuffer, 0, count);
            if (read > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    buffer[offset + i] += _tempBuffer[i];
                }
            }
        }

        // YM2151の波形を生成してミックスする
        if (_ym2151Buffer == null || _ym2151Buffer.Length < count)
        {
            _ym2151Buffer = new float[count];
            _ym2151IntBuffer = new int[2][];
            _ym2151IntBuffer[0] = new int[count]; // L
            _ym2151IntBuffer[1] = new int[count]; // R
        }

        // YM2151Coreからの出力はStereoだが、今回は簡易的にL/RをミックスしてMono出力する
        // （ステレオ出力対応する場合はWaveFormatを2chに変更する必要がある）
        YM2151.GenerateSamples(_ym2151IntBuffer, count);

        // YM2151の出力を合成（YM2151の内部出力は値が大きいので適度にスケールする）
        const float ym2151VolumeScale = 1.0f / 32768.0f; // 16bit相当と仮定
        for (int i = 0; i < count; i++)
        {
            // LとRを平均してモノラル化
            float ymSample = (_ym2151IntBuffer[0][i] + _ym2151IntBuffer[1][i]) * 0.5f * ym2151VolumeScale;
            buffer[offset + i] += ymSample;
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
