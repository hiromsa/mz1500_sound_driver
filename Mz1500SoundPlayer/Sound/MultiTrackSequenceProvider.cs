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

    // YM2151のレジスタ書き込みと波形生成を同期させるためのフレームサイズ
    // 60fps = 44100 / 60 ≒ 735サンプル
    private int SamplesPerFrame => WaveFormat.SampleRate / 60;

    public int Read(float[] buffer, int offset, int count)
    {
        if (_tempBuffer == null || _tempBuffer.Length < count)
        {
            _tempBuffer = new float[count];
        }
        
        // 最終出力バッファをゼロクリア
        Array.Clear(buffer, offset, count);

        // --- PSGトラック（MmlSequenceProvider）は従来通り一括Read ---
        // PSGは自身のRead()内でサンプル単位に波形生成が完結しているため問題なし
        foreach (var item in _trackProviders)
        {
            var provider = item.Provider;
            bool isMuted = !ActiveChannels.Contains(item.TrackName);

            if (provider is MmlSequenceProvider m)
            {
                m.IsMuted = isMuted;
                int read = provider.Read(_tempBuffer, 0, count);
                if (read > 0)
                {
                    for (int i = 0; i < read; i++)
                    {
                        buffer[offset + i] += _tempBuffer[i];
                    }
                }
            }
            else if (provider is Ym2151SequenceProvider y)
            {
                y.IsMuted = isMuted;
                // FMトラックのVM実行は下のフレーム単位ループ内で行う
            }
        }

        // --- FM音源: フレーム単位チャンク処理 ---
        // VM実行（レジスタ書き込み）と波形生成を1フレーム(≒735サンプル)ごとに交互実行し、
        // ノートごとのレジスタ変更が波形に即座に反映されるようにする
        int samplesPerFrame = SamplesPerFrame;
        int processed = 0;

        while (processed < count)
        {
            int chunkSize = Math.Min(samplesPerFrame, count - processed);

            // 1) FMトラックのVM実行（このチャンク分のサンプル進行でレジスタが更新される）
            foreach (var item in _trackProviders)
            {
                if (item.Provider is Ym2151SequenceProvider y)
                {
                    // Read()内でサンプルカウントに応じてProcessVM()が呼ばれ、レジスタが書き込まれる
                    // バッファには0fが書かれるだけなので出力値は使わない
                    y.Read(_tempBuffer, 0, chunkSize);
                }
            }

            // 2) このチャンク分のYM2151波形を生成
            if (_ym2151IntBuffer == null || _ym2151IntBuffer[0].Length < chunkSize)
            {
                _ym2151IntBuffer = new int[2][];
                _ym2151IntBuffer[0] = new int[samplesPerFrame]; // L
                _ym2151IntBuffer[1] = new int[samplesPerFrame]; // R
            }

            // 前回の残留データをクリア
            Array.Clear(_ym2151IntBuffer[0], 0, chunkSize);
            Array.Clear(_ym2151IntBuffer[1], 0, chunkSize);

            YM2151.GenerateSamples(_ym2151IntBuffer, chunkSize);

            // 3) YM2151の出力をメインバッファに合成
            const float ym2151VolumeScale = 1.0f / 32768.0f; // 16bit相当と仮定
            for (int i = 0; i < chunkSize; i++)
            {
                // LとRを平均してモノラル化
                float ymSample = (_ym2151IntBuffer[0][i] + _ym2151IntBuffer[1][i]) * 0.5f * ym2151VolumeScale;
                buffer[offset + processed + i] += ymSample;
            }

            processed += chunkSize;
        }

        // オーバーフロー(クリッピング)の簡易防止 (本来はリミッター等が望ましい)
        for (int i = 0; i < count; i++)
        {
            if (buffer[offset + i] > 1.0f) buffer[offset + i] = 1.0f;
            else if (buffer[offset + i] < -1.0f) buffer[offset + i] = -1.0f;
        }

        return count;
    }
}
