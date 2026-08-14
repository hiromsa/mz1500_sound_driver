using System;

namespace Mz1500SoundPlayer.Sound
{
    /// <summary>
    /// YM2151コアのラッパーおよびI/Oポート(0708h, 0709h)エミュレーションを提供します。
    /// </summary>
    public class YM2151Manager
    {
        public const uint Clock4MHz = 4000000;   // 4.0 MHz (標準)
        public const uint Clock358MHz = 3579545; // 3.58 MHz (切り替え可能)

        private readonly YM2151Core _core;
        private readonly int _sampleRate;
        private uint _clock;
        private byte _addressRegister;
        private byte _statusRegister; // Bit 7: BUSY, Bit 1: Timer B, Bit 0: Timer A

        public uint Clock => _clock;

        public YM2151Manager(int sampleRate = 44100, uint clock = Clock4MHz)
        {
            _sampleRate = sampleRate;
            _clock = clock;
            _core = new YM2151Core();
            _core.Start(0, (uint)sampleRate, _clock);
            Reset();
        }

        /// <summary>
        /// 動作クロック周波数を変更します (例: Clock4MHz, Clock358MHz)
        /// </summary>
        public void SetClock(uint clock)
        {
            _clock = clock;
            _core.Start(0, (uint)_sampleRate, _clock);
        }

        public void Reset()
        {
            _core.Reset(0);
            _addressRegister = 0;
            _statusRegister = 0; // BUSY is 0 (Ready)
        }

        /// <summary>
        /// I/Oポートへの出力エミュレーション
        /// 0708h : アドレスポート
        /// 0709h : データポート
        /// </summary>
        public void OutPort(ushort port, byte data)
        {
            if (port == 0x0708)
            {
                _addressRegister = data;
            }
            else if (port == 0x0709)
            {
                _core.Write(0, 0, _addressRegister, data);
            }
        }

        /// <summary>
        /// I/Oポートからの入力エミュレーション
        /// 0708h : 未定義(常に00hを返す)
        /// 0709h : ステータスポート
        /// </summary>
        public byte InPort(ushort port)
        {
            if (port == 0x0708)
            {
                return 0x00;
            }
            else if (port == 0x0709)
            {
                // エミュレータなので基本的に常にBUSY解除(0)を返す
                // 実機で必要なBUSYウェイトループを通過できるようにする
                return _statusRegister; 
            }
            return 0xFF;
        }

        /// <summary>
        /// 指定サンプル数分のオーディオ波形を生成します。
        /// </summary>
        public void GenerateSamples(int[][] outputs, int samples)
        {
            _core.Update(0, outputs, samples);
        }
    }
}
