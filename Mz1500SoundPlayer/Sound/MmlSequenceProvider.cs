using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace Mz1500SoundPlayer.Sound;

public class MmlSequenceProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; }

    private readonly List<NoteEvent> _sequence;
    private int _noteIndex;
    private double _phase;
    private double _phaseIncrement;
    
    // サンプル単位でのカウンタ
    private long _samplesCurrentNoteTotal;
    private long _samplesCurrentNoteGate;
    private long _currentSampleCount;

    private readonly Dictionary<int, List<int>> _envelopes;

    public MmlSequenceProvider(List<NoteEvent> sequence, Dictionary<int, List<int>> envelopes, int sampleRate = 44100)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _sequence = sequence;
        _envelopes = envelopes ?? new Dictionary<int, List<int>>();
        Reset();
    }

    public void Reset()
    {
        _noteIndex = 0;
        _phase = 0;
        LoadNextNote();
    }

    private void LoadNextNote()
    {
        if (_noteIndex < _sequence.Count)
        {
            var note = _sequence[_noteIndex];
            _phaseIncrement = note.Frequency / WaveFormat.SampleRate;
            
            // ms -> サンプル数への変換
            _samplesCurrentNoteTotal = (long)(note.DurationMs * WaveFormat.SampleRate / 1000.0);
            _samplesCurrentNoteGate = (long)(note.GateTimeMs * WaveFormat.SampleRate / 1000.0);
            _currentSampleCount = 0;
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesWritten = 0;
        // 定数: 1フレーム(60Hz)あたりのサンプル数
        long samplesPer60HzFrame = (long)(WaveFormat.SampleRate / 60.0);

        while (samplesWritten < count)
        {
            if (_noteIndex >= _sequence.Count)
            {
                // 再生終了後は無音で埋める (Stopが呼ばれるまで)
                buffer[offset + samplesWritten] = 0f;
                samplesWritten++;
                continue;
            }

            var note = _sequence[_noteIndex];

            // Gateタイム区間内なら音を鳴らし、超えたら休符にする
            float sampleValue = 0f;
            if (_currentSampleCount < _samplesCurrentNoteGate && note.Frequency > 0 && note.Volume > 0)
            {
                // ソフトウェアエンベロープの適用
                double activeVol = note.Volume;
                if (note.EnvelopeId >= 0 && _envelopes.TryGetValue(note.EnvelopeId, out var envData) && envData.Count > 0)
                {
                    // 現在の経過フレームインデックス = _currentSampleCount / samplesPer60HzFrame
                    int frameIndex = (int)(_currentSampleCount / samplesPer60HzFrame);
                    if (frameIndex >= envData.Count) frameIndex = envData.Count - 1; // 0xFFの場合は末尾でループを止める簡易挙動相当
                    
                    // エンベロープ配列は 0-15。0.15のスケーリングに合わせる (0-15 * 0.01)
                    double evVolRaw = envData[frameIndex];
                    if (evVolRaw == 255) { evVolRaw = envData[^1]; } // End marker -> keep last val (簡単な対処)
                    
                    activeVol = (evVolRaw / 15.0) * 0.15;
                }

                // 矩形波生成
                float rawWave = (float)((_phase < 0.5) ? activeVol : -activeVol);
                
                // ポップノイズ防止の簡易補間 (Attack: 50 , Release: 200 samples)
                float envelopeFilter = 1.0f;
                long samplesFromEnd = _samplesCurrentNoteGate - _currentSampleCount;
                if (_currentSampleCount < 50) envelopeFilter = _currentSampleCount / 50.0f;
                else if (samplesFromEnd < 200) envelopeFilter = samplesFromEnd / 200.0f;

                sampleValue = rawWave * envelopeFilter;
                
                _phase += _phaseIncrement;
                if (_phase >= 1.0) _phase -= 1.0;
            }

            buffer[offset + samplesWritten] = sampleValue;
            samplesWritten++;
            _currentSampleCount++;

            // 次のノートへ移行
            if (_currentSampleCount >= _samplesCurrentNoteTotal)
            {
                _noteIndex++;
                LoadNextNote();
            }
        }

        return count; // 常に要求分を返す(無限あるいは親で止めるまで)
    }
}
