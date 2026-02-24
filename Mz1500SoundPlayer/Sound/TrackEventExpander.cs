using System;
using System.Collections.Generic;

namespace Mz1500SoundPlayer.Sound;

public class TrackEventExpander
{
    private readonly int _sampleRate;

    public TrackEventExpander(int sampleRate = 44100)
    {
        _sampleRate = sampleRate;
    }

    public List<NoteEvent> Expand(TrackData track)
    {
        var events = new List<NoteEvent>();
        
        int currentTempo = 120; // 4分音符=120
        int defaultLength = 4;
        int currentOctave = 4;
        int currentVolume = 15; // 0-15
        int currentEnvelopeId = -1; // -1 = off
        int currentPitchEnvelopeId = -1; // -1 = off
        int currentQuantize = 7; // MCK/PPMCKに合わせデフォルトをやや短く(7/8など)して音の区切りをつける
        int frameQuantize = 0; // @q

        int currentNoiseWaveMode = 1; // @wn1
        int currentIntegrateNoiseMode = 0; // @in0
        int currentDetune = 0; // D (cents)

        // ループ処理用スタック等 (今回は簡易的にフラット展開する)
        var flatCommands = FlattenLoops(track.Commands);

        bool nextIsLoopPoint = false;

        foreach (var cmd in flatCommands)
        {
            if (cmd is InfiniteLoopPointCommand) { nextIsLoopPoint = true; }
            else if (cmd is TempoCommand tc) { currentTempo = tc.Tempo; }
            else if (cmd is FrameTempoCommand ftc) 
            {
                // @t <len>,<frames> 
                // frames * (1/60) sec = (len分音符) の長さになるテンポ
                // tempo = (14400 / frames / len) -> ※1分間(60*60=3600flm) に len分音符(4/len quarter)がいくつ入るか
                // 簡易実装として近いTempoに換算
                if (ftc.FrameCount > 0 && ftc.Length > 0)
                {
                    currentTempo = 14400 / ftc.FrameCount / ftc.Length; 
                }
            }
            else if (cmd is DefaultLengthCommand dlc) { defaultLength = dlc.Length; }
            else if (cmd is OctaveCommand oc) { currentOctave = oc.Octave; }
            else if (cmd is RelativeOctaveCommand rc) { currentOctave += rc.Offset; }
            else if (cmd is VolumeCommand vc) { currentVolume = vc.Volume; } // (0-15)
            else if (cmd is EnvelopeCommand evc) { currentEnvelopeId = evc.EnvelopeId; }
            else if (cmd is PitchEnvelopeCommand pevc) 
            { 
                currentPitchEnvelopeId = pevc.EnvelopeId == 255 ? -1 : pevc.EnvelopeId; 
            }
            else if (cmd is QuantizeCommand qc) { currentQuantize = qc.Quantize; }
            else if (cmd is FrameQuantizeCommand fqc) { frameQuantize = fqc.Frames; }
            else if (cmd is NoiseWaveCommand nwc) { currentNoiseWaveMode = nwc.WaveType; }
            else if (cmd is IntegrateNoiseCommand inc) { currentIntegrateNoiseMode = inc.IntegrateMode; }
            else if (cmd is DetuneCommand dc) { currentDetune = dc.Detune; }
            else if (cmd is TieCommand tieCmd)
            {
                if (events.Count > 0)
                {
                    // 直前の音符の長さを延長する
                    int len = tieCmd.Length == 0 ? defaultLength : tieCmd.Length;
                    double quarterNoteMs = 60000.0 / currentTempo;
                    double durationMs = (quarterNoteMs * 4.0) / len;
                    if (tieCmd.Dots > 0)
                    {
                        double add = durationMs / 2.0;
                        for (int i = 0; i < tieCmd.Dots; i++) { durationMs += add; add /= 2.0; }
                    }

                    var lastEvent = events[^1];
                    // タイなのでGateTimeは「延長後の全体長」に対して再計算するか、単純にDurationと同じだけ増やす
                    // ここでは後ろに繋がるためGateは事実上「100%」相当で繋げる
                    double newGateMs = lastEvent.GateTimeMs + durationMs; 

                    events[^1] = lastEvent with 
                    { 
                        DurationMs = lastEvent.DurationMs + durationMs,
                        GateTimeMs = newGateMs
                    };
                }
            }
            else if (cmd is NoteCommand nc)
            {
                int len = nc.Length == 0 ? defaultLength : nc.Length;
                
                // 長さを ms で計算 ( tempo = 四分音符/min )
                double quarterNoteMs = 60000.0 / currentTempo;
                double durationMs = (quarterNoteMs * 4.0) / len;
                
                // 付点の計算
                if (nc.Dots > 0)
                {
                    double add = durationMs / 2.0;
                    for (int i=0; i<nc.Dots; i++) { durationMs += add; add /= 2.0; }
                }

                // ゲートタイム (発音時間) の計算
                double gateMs = durationMs;
                if (frameQuantize > 0)
                {
                    // @q (フレーム数分短く)
                    gateMs -= frameQuantize * (1000.0 / 60.0);
                }
                else
                {
                    // q (8分率)
                    gateMs = durationMs * (currentQuantize / 8.0);
                }
                if (gateMs < 0) gateMs = 0;

                if (nc.Note == 'r')
                {
                    events.Add(new NoteEvent(0, durationMs, 0, 0, currentEnvelopeId, currentPitchEnvelopeId, currentNoiseWaveMode, currentIntegrateNoiseMode, nextIsLoopPoint));
                }
                else
                {
                    double freq = GetFrequency(nc.Note, nc.SemiToneOffset, currentOctave);
                    if (currentDetune != 0)
                    {
                        freq = freq * Math.Pow(2.0, currentDetune / 1200.0);
                    }
                    // 音量を 0.0 - 0.2 くらいにスケーリング
                    double vol = (currentVolume / 15.0) * 0.15;
                    events.Add(new NoteEvent(freq, durationMs, vol, gateMs, currentEnvelopeId, currentPitchEnvelopeId, currentNoiseWaveMode, currentIntegrateNoiseMode, nextIsLoopPoint));
                }
                nextIsLoopPoint = false;
            }
        }

        // 行末などにLだけ置いて終わった場合、終端フラグを立たせるために長さ0のダミー休符を置く
        if (nextIsLoopPoint)
        {
            events.Add(new NoteEvent(0, 0, 0, 0, currentEnvelopeId, currentPitchEnvelopeId, currentNoiseWaveMode, currentIntegrateNoiseMode, true));
        }

        return events;
    }

    private List<MmlCommand> FlattenLoops(List<MmlCommand> source)
    {
        // 最も簡単な[ ] の展開 (ネストは再帰的に処理)
        var result = new List<MmlCommand>();
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] is LoopBeginCommand)
            {
                int nest = 1;
                int j = i + 1;
                var inner = new List<MmlCommand>();
                while (j < source.Count && nest > 0)
                {
                    if (source[j] is LoopBeginCommand) nest++;
                    else if (source[j] is LoopEndCommand) nest--;
                    
                    if (nest > 0) inner.Add(source[j]);
                    j++;
                }
                
                int loopCount = 2; // default
                if (j - 1 < source.Count && source[j - 1] is LoopEndCommand le)
                {
                    loopCount = le.Count > 0 ? le.Count : 2;
                }

                var expandedInner = FlattenLoops(inner);
                for (int c = 0; c < loopCount; c++)
                {
                    // PPMCK仕様の '|' (最後のループで以降をスキップ) は未検証(今回はフラットに追加)
                    result.AddRange(expandedInner);
                }
                i = j - 1; // skip loop block
            }
            else
            {
                result.Add(source[i]);
            }
        }
        return result;
    }

    private double GetFrequency(char note, int semiToneOffset, int octave)
    {
        int noteIndex = note switch
        {
            'c' => 0, 'd' => 2, 'e' => 4, 'f' => 5, 'g' => 7, 'a' => 9, 'b' => 11,
            _ => 0
        };
        noteIndex += semiToneOffset;
        int semitonesFromA4 = noteIndex - 9 + (octave - 4) * 12;
        return 440.0 * Math.Pow(2.0, semitonesFromA4 / 12.0);
    }
}
