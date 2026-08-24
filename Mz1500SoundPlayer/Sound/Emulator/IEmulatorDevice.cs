namespace Mz1500SoundPlayer.Sound.Emulator
{
    /// <summary>
    /// メモリやI/Oにマップされるエミュレータ上のデバイスの共通インターフェース
    /// </summary>
    public interface IEmulatorDevice
    {
        /// <summary>
        /// デバイスをリセットします。
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// メモリ空間にマップされるデバイス
    /// </summary>
    public interface IMemoryDevice : IEmulatorDevice
    {
        byte ReadMemory(ushort address);
        void WriteMemory(ushort address, byte data);
    }

    /// <summary>
    /// I/Oポート空間にマップされるデバイス
    /// </summary>
    public interface IIoDevice : IEmulatorDevice
    {
        byte ReadIo(byte port);
        void WriteIo(byte port, byte data);
    }
}
