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

    public MmlSequenceProvider(List<NoteEvent> sequence, int sampleRate = 44100)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _sequence = sequence;
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
                // 矩形波生成
                float rawWave = (float)((_phase < 0.5) ? note.Volume : -note.Volume);
                
                // ポップノイズ防止の簡易エンベロープ (Attack: 50 , Release: 200 samples)
                float envelope = 1.0f;
                long samplesFromEnd = _samplesCurrentNoteGate - _currentSampleCount;
                if (_currentSampleCount < 50) envelope = _currentSampleCount / 50.0f;
                else if (samplesFromEnd < 200) envelope = samplesFromEnd / 200.0f;

                sampleValue = rawWave * envelope;
                
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
