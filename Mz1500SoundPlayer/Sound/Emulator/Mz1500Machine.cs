using System;
using Konamiman.Z80dotNet;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class Mz1500Machine
    {
        public Z80Processor Cpu { get; private set; }
        public Mz1500Memory Memory { get; private set; }
        public IoController Io { get; private set; }
        public Keyboard Keyboard { get; private set; }
        
        private Intel8253 _pit;
        private Intel8255 _pio;
        private Sn76489an _psgL;
        private Sn76489an _psgR;
        private YM2151Core _opm;

        private byte _pioPortCData = 0;
        private bool _vblank = false;
        private bool _blink = false;

        public byte[]? CgRom => Memory.CgRom;

        public Mz1500Machine()
        {
            Cpu = new Z80Processor();
            Memory = new Mz1500Memory();
            Io = new IoController();
            Keyboard = new Keyboard();

            Cpu.Memory = Memory;
            Cpu.PortsSpace = Io;
            
            // Setup Devices
            _pit = new Intel8253();
            _pio = new Intel8255();
            _psgL = new Sn76489an();
            _psgR = new Sn76489an();
            _opm = new YM2151Core();
            _opm.Start(0, 44100, 4000000);
            
            var beep = new Beep();

            // Connect 8255 to Keyboard and signals
            // Port A (Out): Strobe (lower 4 bits select matrix row)
            _pio.OnPortAWrite = (data) => Keyboard.SetStrobe(data);
            // Port B (In): Matrix data for selected row
            _pio.OnPortBRead = () => Keyboard.ReadMatrix();
            // Port C (In/Out):
            // PC7: VBLANK (active low or high), PC6: 1.5kHz Blink, PC4: Motor remote (1 = off), PC0: Beep
            _pio.OnPortCRead = () =>
            {
                byte val = (byte)(_pioPortCData & 0x0F);
                if (!_vblank) val |= 0x80; // Display period: Bit 7 = 1, VBLANK: Bit 7 = 0
                if (_blink) val |= 0x40;  // 1.5kHz blink
                val |= 0x10;              // Motor remote state
                return val;
            };
            _pio.OnPortCWrite = (data) =>
            {
                _pioPortCData = data;
                beep.SetOn((data & 0x01) != 0);
            };

            // Connect memory-mapped I/O devices
            Memory.SetDevices(_pio, _pit);

            // Register standard I/O (IN/OUT) ports
            // Memory Banking / Palette ports
            for (byte p = 0xE0; p <= 0xE6; p++) Io.RegisterDevice(p, Memory);
            Io.RegisterDevice(0xE8, Memory);
            Io.RegisterDevice(0xF0, Memory);
            Io.RegisterDevice(0xF1, Memory);

            // PSG ports
            Io.RegisterDevice(0xE9, new PsgBothWrapper(_psgL, _psgR));
            Io.RegisterDevice(0xF2, _psgL);
            Io.RegisterDevice(0xF3, _psgR);

            // YM2151 ports (0x08/0x09)
            Io.RegisterDevice(0x08, new Ym2151Wrapper(_opm, 0)); // Address
            Io.RegisterDevice(0x09, new Ym2151Wrapper(_opm, 1)); // Data

            // Try loading MONITOR ROM
            LoadRom();
        }

        private class PsgBothWrapper : IIoDevice
        {
            private readonly Sn76489an _left;
            private readonly Sn76489an _right;
            public PsgBothWrapper(Sn76489an left, Sn76489an right) { _left = left; _right = right; }
            public void Reset() { }
            public byte ReadIo(byte port) => 0xFF;
            public void WriteIo(byte port, byte data) { _left.WriteIo(port, data); _right.WriteIo(port, data); }
        }
        
        private class Ym2151Wrapper : IIoDevice
        {
            private readonly YM2151Core _core;
            private readonly int _type; // 0=Addr, 1=Data
            public Ym2151Wrapper(YM2151Core core, int type) { _core = core; _type = type; }
            public void Reset() { }
            public byte ReadIo(byte port) => 0;
            public void WriteIo(byte port, byte data) => _core.Write(0, _type, 0, data);
        }
        
        private void LoadRom()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string romDir = System.IO.Path.Combine(baseDir, "..", "..", "..", "romsample");
                if (!System.IO.Directory.Exists(romDir))
                {
                    romDir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "romsample");
                }

                string iplPath = System.IO.Path.Combine(romDir, "IPL.ROM");
                if (System.IO.File.Exists(iplPath))
                {
                    byte[] romData = System.IO.File.ReadAllBytes(iplPath);
                    // Load IPL.ROM (first 4KB goes to 0x0000)
                    byte[] ipl = new byte[4096];
                    Array.Copy(romData, 0, ipl, 0, Math.Min(4096, romData.Length));
                    Memory.LoadRom(0x0000, ipl);
                    
                    if (romData.Length > 4096)
                    {
                        int extSize = romData.Length - 4096;
                        byte[] ext = new byte[extSize];
                        Array.Copy(romData, 4096, ext, 0, extSize);
                        Memory.LoadRom(0xE800, ext);
                    }
                    Console.WriteLine($"Loaded Monitor ROM: {iplPath}");
                }
                else
                {
                    Console.WriteLine("Monitor ROM (IPL.ROM) not found.");
                }

                string fontPath = System.IO.Path.Combine(romDir, "FONT.ROM");
                if (System.IO.File.Exists(fontPath))
                {
                    Memory.CgRom = System.IO.File.ReadAllBytes(fontPath);
                    Console.WriteLine($"Loaded CGROM: {fontPath}");
                }
                else
                {
                    Memory.CgRom = new byte[0x1000];
                    Console.WriteLine("CGROM (FONT.ROM) not found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load ROMs: {ex.Message}");
            }
        }

        public void Reset()
        {
            Cpu.Reset();
            Memory.Reset();
            _pit.Reset();
            _pio.Reset();
            _psgL.Reset();
            _psgR.Reset();
            Keyboard.Reset();
        }

        public void LoadZ80Binary(ushort address, byte[] data)
        {
            Memory.LoadBinary(address, data);
        }

        public bool LoadMzt(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    Console.WriteLine($"MZT file not found: {path}");
                    return false;
                }

                byte[] data = System.IO.File.ReadAllBytes(path);
                if (data.Length < 128)
                {
                    Console.WriteLine("Invalid MZT file (too small).");
                    return false;
                }

                ushort size = (ushort)(data[0x12] | (data[0x13] << 8));
                ushort loadAddr = (ushort)(data[0x14] | (data[0x15] << 8));
                ushort execAddr = (ushort)(data[0x16] | (data[0x17] << 8));

                if (data.Length < 128 + size)
                {
                    Console.WriteLine($"Invalid MZT file: Expected {size} bytes, got {data.Length - 128} bytes.");
                    return false;
                }

                byte[] bin = new byte[size];
                Array.Copy(data, 128, bin, 0, size);
                Memory.LoadBinary(loadAddr, bin);

                Console.WriteLine($"Loaded MZT: Load Addr={loadAddr:X4}, Exec Addr={execAddr:X4}, Size={size}");
                
                // Set PC to the execution address
                Cpu.Registers.PC = execAddr;
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load MZT: {ex.Message}");
                return false;
            }
        }

        public bool LoadQdf(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    Console.WriteLine($"QDF file not found: {path}");
                    return false;
                }

                byte[] data = System.IO.File.ReadAllBytes(path);
                int ptr = 0;

                // Skip optional "-QD format-" header
                if (data.Length > 16 && data[0] == '-' && data[1] == 'Q' && data[2] == 'D')
                {
                    ptr = 16;
                }

                ushort lastExecAddr = 0x1200;
                bool loadedAny = false;

                while (ptr < data.Length - 4)
                {
                    if (data[ptr] != 0xA5)
                    {
                        ptr++;
                        continue;
                    }

                    ptr++; // skip 0xA5
                    byte blockType = data[ptr++];
                    ushort blockSize = (ushort)(data[ptr] | (data[ptr + 1] << 8));
                    ptr += 2;

                    if (blockType == 0x00 || blockType == 0x02) // Header Block
                    {
                        if (ptr + blockSize > data.Length) break;

                        byte fileAttr = data[ptr];
                        byte[] nameBytes = new byte[17];
                        Array.Copy(data, ptr + 1, nameBytes, 0, 17);
                        string fileName = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\r', '\0', ' ');

                        ushort fileSize = (ushort)(data[ptr + 20] | (data[ptr + 21] << 8));
                        ushort loadAddr = (ushort)(data[ptr + 22] | (data[ptr + 23] << 8));
                        ushort execAddr = (ushort)(data[ptr + 24] | (data[ptr + 25] << 8));

                        Console.WriteLine($"QDF Header: Name='{fileName}', Size=0x{fileSize:X4}, LoadAddr=0x{loadAddr:X4}, ExecAddr=0x{execAddr:X4}");

                        ptr += blockSize;
                        ptr += 2; // skip CRC

                        if (execAddr != 0)
                        {
                            lastExecAddr = execAddr;
                        }

                        // Search for accompanying data block
                        while (ptr < data.Length - 4)
                        {
                            if (data[ptr] == 0xA5)
                            {
                                ptr++;
                                byte dBlockType = data[ptr++];
                                ushort dBlockSize = (ushort)(data[ptr] | (data[ptr + 1] << 8));
                                ptr += 2;

                                if (dBlockType == 0x01 || dBlockType == 0x03 || dBlockType == 0x05 || dBlockType == 0x07)
                                {
                                    if (ptr + dBlockSize <= data.Length)
                                    {
                                        byte[] fileData = new byte[Math.Min((int)fileSize, (int)dBlockSize)];
                                        Array.Copy(data, ptr, fileData, 0, fileData.Length);

                                        Memory.LoadBinary(loadAddr, fileData);
                                        Console.WriteLine($"QDF Data: Loaded {fileData.Length} bytes at 0x{loadAddr:X4}");
                                        loadedAny = true;

                                        ptr += dBlockSize;
                                        ptr += 2; // skip CRC
                                        break;
                                    }
                                }
                            }
                            ptr++;
                        }
                    }
                    else
                    {
                        ptr += blockSize;
                        ptr += 2; // skip CRC
                    }
                }

                if (loadedAny)
                {
                    // Set standard QD execution environment:
                    // Bank 0x0000..0x0FFF to RAM (Port 0xE0)
                    // Bank 0xE800..0xFFFF to RAM (Port 0xE2)
                    // Bank 0xD000..0xDFFF to VRAM (Port 0xE4)
                    Memory.WriteIo(0xE0, 0);
                    Memory.WriteIo(0xE2, 0);
                    Memory.WriteIo(0xE4, 0);
                    Memory.WriteIo(0xF0, 0);
                    Memory.WriteIo(0xF1, 0);

                    Cpu.Registers.PC = lastExecAddr;
                    Console.WriteLine($"QDF Loaded successfully. Starting PC = 0x{lastExecAddr:X4}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load QDF: {ex.Message}");
                return false;
            }
        }

        private class TimerInterruptSource : IZ80InterruptSource
        {
            public bool IntLineIsActive => _pending;
            public byte? ValueOnDataBus => 0xFF; // Mode 1/2 dummy vector
            
            public event EventHandler? NmiInterruptPulse;

            private bool _pending = false;

            public void Fire()
            {
                _pending = true;
            }

            public void InterruptAcknowledge()
            {
                _pending = false;
            }
        }

        private ulong _lastIntTStates = 0;
        private ulong _lastTempoTStates = 0;
        private ulong _lastBlinkTStates = 0;
        private ulong _lastPitCh0TStates = 0;
        private ulong _lastPitCh1TStates = 0;

        // Main CPU Run Loop
        public void Run()
        {
            Cpu.ClockFrequencyInMHz = 4.0m;

            var intSource = new TimerInterruptSource();
            Cpu.RegisterInterruptSource(intSource);

            Cpu.AfterInstructionExecution += (sender, args) =>
            {
                ulong currentTStates = Cpu.TStatesElapsedSinceStart;
                
                // PIT Channel 0 (894.886kHz -> every ~4 T-states at 4.0MHz)
                if (currentTStates - _lastPitCh0TStates >= 4)
                {
                    _lastPitCh0TStates = currentTStates;
                    _pit.TickChannel0();
                }

                // PIT Channel 1 (BLANK 15.7kHz -> every ~255 T-states at 4.0MHz)
                // Channel 1 OUT automatically cascades and clocks Channel 2
                if (currentTStates - _lastPitCh1TStates >= 255)
                {
                    _lastPitCh1TStates = currentTStates;
                    _pit.TickChannel1();
                }

                // 32kHz TEMPO signal (every ~125 T-states at 4MHz)
                if (currentTStates - _lastTempoTStates >= 125)
                {
                    _lastTempoTStates = currentTStates;
                    Memory.ToggleTempo();
                }

                // 1.5kHz Blink signal (every ~2666 T-states)
                if (currentTStates - _lastBlinkTStates >= 2666)
                {
                    _lastBlinkTStates = currentTStates;
                    _blink = !_blink;
                }

                // 60Hz VBLANK and INT interrupt (4MHz / 60 = 66666 T-states)
                if (currentTStates - _lastIntTStates >= 66666)
                {
                    _lastIntTStates = currentTStates;
                    _vblank = true;
                    intSource.Fire();
                }
                else if (currentTStates - _lastIntTStates >= 10000)
                {
                    _vblank = false;
                }
            };
            
            Cpu.Continue();
        }
    }
}
