using System;
using System.Collections.Generic;
using Konamiman.Z80dotNet;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class IoController : IMemory
    {
        private Dictionary<byte, IIoDevice> _portMap = new Dictionary<byte, IIoDevice>();
        
        public int Size => 0x10000;

        public void RegisterDevice(byte port, IIoDevice device)
        {
            _portMap[port] = device;
        }

        public byte[] GetContents(int startAddress, int length)
        {
            var result = new byte[length];
            for (int i = 0; i < length; i++) result[i] = GetByte(startAddress + i);
            return result;
        }

        public void SetContents(int startAddress, byte[] contents, int startIndex = 0, int? length = null)
        {
            int len = length ?? (contents.Length - startIndex);
            for (int i = 0; i < len; i++) SetByte(startAddress + i, contents[startIndex + i]);
        }

        public byte this[int address]
        {
            get => GetByte(address);
            set => SetByte(address, value);
        }

        private byte GetByte(int address)
        {
            byte port = (byte)(address & 0xFF);
            if (_portMap.TryGetValue(port, out var device))
            {
                return device.ReadIo(port);
            }
            return 0xFF; // Default disconnected port behavior
        }

        private void SetByte(int address, byte value)
        {
            byte port = (byte)(address & 0xFF);
            if (_portMap.TryGetValue(port, out var device))
            {
                device.WriteIo(port, value);
            }
        }
    }
}
