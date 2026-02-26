using System;
using System.Collections.Generic;

namespace Mz1500SoundPlayer.Sound;

public class MmlError
{
    public int TextStartIndex { get; set; }
    public int Length { get; set; }
    public string Message { get; set; } = "";

    public MmlError(int startIndex, int length, string message)
    {
        TextStartIndex = startIndex;
        Length = length;
        Message = message;
    }
}

public class TrackData
{
    public string Name { get; set; } = "";
    public List<MmlCommand> Commands { get; set; } = new List<MmlCommand>();
}

public class EnvelopeData
{
    public List<int> Values { get; set; } = new();
    public int LoopIndex { get; set; } = -1;
}

public class MmlData
{
    public Dictionary<string, TrackData> Tracks { get; set; } = new();
    public Dictionary<int, EnvelopeData> VolumeEnvelopes { get; set; } = new();
    public Dictionary<int, EnvelopeData> PitchEnvelopes { get; set; } = new();
    public List<MmlError> Errors { get; set; } = new();
}

public abstract class MmlCommand 
{ 
    public int TextStartIndex { get; set; } = -1;
    public int TextLength { get; set; } = 0;
}

// 連符 (Tuplet) コマンド
public class TupletCommand : MmlCommand
{
    public List<MmlCommand> InnerCommands { get; set; } = new();
    public int Length { get; set; } // 全体の長さ
    public int Dots { get; set; }   // 全体の付点
}

// 休符・音符など時間経過を伴うイベント
public class NoteCommand : MmlCommand
{
    public char Note { get; set; } // 'c'-'b', 'r'
    public int SemiToneOffset { get; set; } // +1, -1, 0
    public int Length { get; set; } // MML上の指定長 (4, 8, 16...) 0ならデフォルト長を使用
    public int Dots { get; set; } // 付点の数
}

// 状態変更コマンド
public class TempoCommand : MmlCommand { public int Tempo { get; set; } }
public class FrameTempoCommand : MmlCommand { public int Length { get; set; } public int FrameCount { get; set; } }

public class OctaveCommand : MmlCommand { public int Octave { get; set; } }
public class RelativeOctaveCommand : MmlCommand { public int Offset { get; set; } }

public class DefaultLengthCommand : MmlCommand { public int Length { get; set; } }

public class VolumeCommand : MmlCommand { public int Volume { get; set; } }

// エンベロープマクロ呼び出し (@v1 等)
public class EnvelopeCommand : MmlCommand { public int EnvelopeId { get; set; } }

// ピッチエンベロープマクロ呼び出し (EP1, @p1 等)
public class PitchEnvelopeCommand : MmlCommand { public int EnvelopeId { get; set; } }

// 音長減算用パラメータ (タイ)
public class TieCommand : MmlCommand { public int Length { get; set; } public int Dots { get; set; } }

public class QuantizeCommand : MmlCommand { public int Quantize { get; set; } }
public class FrameQuantizeCommand : MmlCommand { public int Frames { get; set; } }

public class VoiceCommand : MmlCommand { public int VoiceId { get; set; } }

// ディチューンコマンド (D<num>) セント単位
public class DetuneCommand : MmlCommand { public int Detune { get; set; } }

// トランスポーズコマンド (K<num>) 半音単位
public class TransposeCommand : MmlCommand { public int Transpose { get; set; } }

// ノイズ関連
public class NoiseWaveCommand : MmlCommand { public int WaveType { get; set; } }
public class IntegrateNoiseCommand : MmlCommand { public int IntegrateMode { get; set; } }

// 音長減算用パラメータ (ex: ~12, -2) ※今回は簡略化のため一旦無視か拡張枠として用意
// ...

// ループ処理用（ネスト対応は後続のシーケンス生成時に展開する）
public class LoopBeginCommand : MmlCommand { }
public class LoopEndCommand : MmlCommand { public int Count { get; set; } }
public class InfiniteLoopPointCommand : MmlCommand { } // L コマンド
