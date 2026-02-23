using System;
using System.Collections.Generic;

namespace Mz1500SoundPlayer.Sound;

public class TrackData
{
    public string Name { get; set; } = "";
    public List<MmlCommand> Commands { get; set; } = new List<MmlCommand>();
}

public abstract class MmlCommand { }

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
// ※@v などのエンベロープマクロは将来拡張用としてここではVolumeと同等に扱うか、無視して保持する
public class EnvelopeCommand : MmlCommand { public int EnvelopeId { get; set; } }

public class QuantizeCommand : MmlCommand { public int Quantize { get; set; } }
public class FrameQuantizeCommand : MmlCommand { public int Frames { get; set; } }

// マクロ・音色関連（今回 @1 などはダミーで保持するか無視する）
public class VoiceCommand : MmlCommand { public int VoiceId { get; set; } }

// 音長減算用パラメータ (ex: ~12, -2) ※今回は簡略化のため一旦無視か拡張枠として用意
// ...

// ループ処理用（ネスト対応は後続のシーケンス生成時に展開する）
public class LoopBeginCommand : MmlCommand { }
public class LoopEndCommand : MmlCommand { public int Count { get; set; } }
public class InfiniteLoopPointCommand : MmlCommand { } // L コマンド
