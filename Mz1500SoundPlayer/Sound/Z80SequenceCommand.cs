namespace Mz1500SoundPlayer.Sound;

/// <summary>
/// Z80逕ｨ繧ｵ繧ｦ繝ｳ繝峨す繝ｼ繧ｱ繝ｳ繧ｹ縺ｮ1繝舌う繝医さ繝槭Φ繝会ｼ域眠莉墓ｧ假ｼ・/// </summary>
public enum Z80SequenceCommand : byte
{
    // 0x00 - 0x5F : Note ON (髻ｳ髫・0・・5)
    // 髻ｳ髫守分蜿ｷ縺ｯ縺昴・縺ｾ縺ｾ繧ｳ繝槭Φ繝峨→縺励※謇ｱ繧上ｌ繧九◆繧√・num蛟､縺ｨ縺励※縺ｯ螳夂ｾｩ縺励↑縺・
    /// <summary>莨醍ｬｦ (Rest)</summary>
    Rest = 0x60,

    // 0x80 - 0x8F : 髟ｷ縺募､画峩 (遏ｭ) - Length 1..16 frames
    ShortLengthBase = 0x80,

    /// <summary>髟ｷ縺募､画峩 (髟ｷ) + 2 bytes</summary>
    LongLength = 0x90,

    /// <summary>髻ｳ濶ｲ/繧ｨ繝ｳ繝吶Ο繝ｼ繝怜､画峩 + 1 byte</summary>
    SetVoice = 0xA0,

    /// <summary>髻ｳ驥丞､画峩 + 1 byte</summary>
    SetVolume = 0xA1,

    /// <summary>繝ｫ繝ｼ繝励・繝ｼ繧ｫ繝ｼ・医％縺薙°繧臥┌髯舌Ν繝ｼ繝暦ｼ・/summary>
    LoopMarker = 0x08, // 莠呈鋤諤ｧ縺ｮ縺溘ａ荳譌ｦ0x08縺ｮ縺ｾ縺ｾ縺ｧ繧ょ庄縲√≠繧九＞縺ｯ螟画峩

    /// <summary>繝医Λ繝・け縺ｮ邨ゅｏ繧奇ｼ育┌髯舌Ν繝ｼ繝礼ｵらｫｯ縺ｪ縺ｩ縺ｫ菴ｿ逕ｨ・・/summary>
    TrackEnd = 0xFF
}
