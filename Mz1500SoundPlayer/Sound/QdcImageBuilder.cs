using System;
using System.Collections.Generic;
using System.Text;

namespace Mz1500SoundPlayer.Sound;

/// <summary>
/// QDC (MZ-1500 Quick Disk) イメージを作成するビルダークラス
/// </summary>
public class QdcImageBuilder
{
    private readonly List<byte> _data = new();
    private const int FILE_SIZE = 81936;

    public void AppendByte(byte b) => _data.Add(b);

    public void AppendBytes(byte[] bytes) => _data.AddRange(bytes);

    public void AppendUShortLE(ushort value)
    {
        _data.Add((byte)(value & 0xFF));
        _data.Add((byte)((value >> 8) & 0xFF));
    }

    public void AppendFillByteToLength(byte b, int targetLength)
    {
        int required = targetLength - _data.Count;
        for (int i = 0; i < required; i++) _data.Add(b);
    }

    public void AppendFillZeroToEnd()
    {
        AppendFillByteToLength(0, FILE_SIZE);
    }

    public void AppendString(string text)
    {
        _data.AddRange(Encoding.GetEncoding(932).GetBytes(text));
    }

    public void AppendFileName(string name)
    {
        const int MAX_LEN = 16;
        if (name.Length > MAX_LEN) name = name.Substring(0, MAX_LEN);
        AppendString(name);
        for (int i = name.Length; i < MAX_LEN; i++) AppendByte(0x0D);
        AppendByte(0x0D);
    }

    public ushort GetCrc16()
    {
        ushort crc = 0;
        foreach (byte b in _data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 1) != 0)
                {
                    crc >>= 1;
                    crc ^= 0xA001;
                }
                else
                {
                    crc >>= 1;
                }
            }
        }
        return crc;
    }

    public byte[] BuildStandardExecutable(string fileName, byte[] executableData)
    {
        _data.Clear();
        AppendString("-QD format-");
        for (int i = 0; i < 5; i++) AppendByte(0xFF);
        for (int i = 0; i < 0x12DA; i++) AppendByte(0);

        AppendStandardSync(); // 0x16 x 10

        // Information Block
        {
            var infoBlock = new QdcImageBuilder();
            infoBlock.AppendByte(0xA5); // Data Start
            infoBlock.AppendByte(2);    // Block Count
            _data.AddRange(infoBlock._data);
            ushort crc = infoBlock.GetCrc16();
            _data.Add((byte)(crc & 0xFF)); // CRC lower
            _data.Add((byte)(crc >> 8));   // CRC upper
        }

        AppendStandardSync();
        for(int i=0; i<0xAEB; i++) AppendByte(0); // Gap

        // Header Block
        {
            AppendByte(0);
            AppendStandardSync();
            var headBlock = new QdcImageBuilder();
            headBlock.AppendByte(0xA5);     // Start
            headBlock.AppendByte(0);
            headBlock.AppendByte(0x40);
            headBlock.AppendByte(0);
            headBlock.AppendByte(0x01);     // FileType: Object
            headBlock.AppendFileName(fileName);
            headBlock.AppendByte(0);
            headBlock.AppendByte(0);
            
            // Fixed length definitions compatible with MZ-1500 (based on VB source)
            headBlock.AppendUShortLE(0xBE00);   // Data Size
            headBlock.AppendUShortLE(0x1200);   // Load Addr (Template default)
            headBlock.AppendUShortLE(0x1200);   // Exec Addr
            headBlock.AppendFillByteToLength(0, 0x44); // Header block requires exact length
            
            _data.AddRange(headBlock._data);
            ushort crc = headBlock.GetCrc16();
            _data.Add((byte)(crc & 0xFF));
            _data.Add((byte)(crc >> 8));
            AppendStandardSync();
        }

        for(int i=0; i<0xFF; i++) AppendByte(0); // Gap

        // Data Block
        {
            AppendStandardSync();
            var dataBlock = new QdcImageBuilder();
            dataBlock.AppendByte(0xA5); // Start
            dataBlock.AppendByte(0x05); // Data type
            dataBlock.AppendUShortLE(0xBE00); // Size
            dataBlock.AppendBytes(executableData); // The actual payload (Z80 bin)
            
            dataBlock.AppendFillByteToLength(0, 0xBE04); // Fixed padding as per temp VB src
            
            _data.AddRange(dataBlock._data);
            ushort crc = dataBlock.GetCrc16();
            _data.Add((byte)(crc & 0xFF));
            _data.Add((byte)(crc >> 8));
            AppendStandardSync();
        }

        AppendFillZeroToEnd();

        return _data.ToArray();
    }

    private void AppendStandardSync()
    {
        for (int i = 0; i < 10; i++) AppendByte(0x16);
    }
}
