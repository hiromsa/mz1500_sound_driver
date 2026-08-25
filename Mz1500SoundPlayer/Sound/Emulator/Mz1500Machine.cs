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
        
        public Sn76489an PsgL => _psgL;
        public Sn76489an PsgR => _psgR;
        public YM2151Core Opm => _opm;

        private Intel8253 _pit;
        private Intel8255 _pio;
        private Z80PioInterruptDevice _pioInt;
        private Sn76489an _psgL;
        private Sn76489an _psgR;
        private YM2151Core _opm;
        private TimerInterruptSource? _intSource;

        public object SoundLock { get; } = new object();

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
            
            _pit = new Intel8253();
            _pio = new Intel8255();
            _psgL = new Sn76489an();
            _psgR = new Sn76489an();
            _opm = new YM2151Core();
            _opm.Start(0, 44100, 4000000);
            
            var beep = new Beep();

            // Connect 8255 to Keyboard and signals
            _pio.OnPortAWrite = (data) => Keyboard.SetStrobe(data);
            _pio.OnPortBRead = () => Keyboard.ReadMatrix();
            _pio.OnPortCRead = () =>
            {
                byte val = (byte)(_pioPortCData & 0x0F);
                if (!_vblank) val |= 0x80;
                if (_blink) val |= 0x40;
                val |= 0x10;
                return val;
            };
            _pio.OnPortCWrite = (data) =>
            {
                _pioPortCData = data;
                beep.SetOn((data & 0x01) != 0);
            };

            Memory.SetDevices(_pio, _pit);

            for (byte p = 0xE0; p <= 0xE6; p++) Io.RegisterDevice(p, Memory);
            Io.RegisterDevice(0xE8, Memory);
            Io.RegisterDevice(0xF0, Memory);
            Io.RegisterDevice(0xF1, Memory);

            Io.RegisterDevice(0xE9, new PsgBothWrapper(_psgL, _psgR, SoundLock));
            Io.RegisterDevice(0xF2, new PsgWrapper(_psgL, SoundLock));
            Io.RegisterDevice(0xF3, new PsgWrapper(_psgR, SoundLock));

            Io.RegisterDevice(0x08, new Ym2151Wrapper(_opm, 0, SoundLock));
            Io.RegisterDevice(0x09, new Ym2151Wrapper(_opm, 1, SoundLock));

            _pioInt = new Z80PioInterruptDevice();
            for (int p = 0xFC; p <= 0xFF; p++) Io.RegisterDevice((byte)p, _pioInt);

            _pit.RegisterInterruptHandler(0, () => _intSource?.FireA());
            _pit.RegisterInterruptHandler(2, () => _intSource?.FireA());

            LoadRom();
        }

        private class PsgWrapper : IIoDevice
        {
            private readonly Sn76489an _psg;
            private readonly object _lock;
            public PsgWrapper(Sn76489an psg, object lk) { _psg = psg; _lock = lk; }
            public void Reset() { lock (_lock) _psg.Reset(); }
            public byte ReadIo(byte port) => 0xFF;
            public void WriteIo(byte port, byte data) { lock (_lock) _psg.WriteIo(port, data); }
        }

        private class PsgBothWrapper : IIoDevice
        {
            private readonly Sn76489an _left;
            private readonly Sn76489an _right;
            private readonly object _lock;
            public PsgBothWrapper(Sn76489an left, Sn76489an right, object lk) { _left = left; _right = right; _lock = lk; }
            public void Reset() { lock (_lock) { _left.Reset(); _right.Reset(); } }
            public byte ReadIo(byte port) => 0xFF;
            public void WriteIo(byte port, byte data) { lock (_lock) { _left.WriteIo(port, data); _right.WriteIo(port, data); } }
        }
        
        private class Ym2151Wrapper : IIoDevice
        {
            private readonly YM2151Core _core;
            private readonly int _type; // 0=Addr, 1=Data
            private readonly object _lock;
            public Ym2151Wrapper(YM2151Core core, int type, object lk) { _core = core; _type = type; _lock = lk; }
            public void Reset() { }
            public byte ReadIo(byte port) => 0;
            public void WriteIo(byte port, byte data) { lock (_lock) _core.Write(0, _type, 0, data); }
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
                bool isFirstBlock = true;

                ushort currentLoadAddr = 0;
                ushort currentFileSize = 0;

                while (ptr < data.Length - 4)
                {
                    if (data[ptr] != 0xA5)
                    {
                        ptr++;
                        continue;
                    }

                    ptr++; // skip 0xA5

                    if (isFirstBlock)
                    {
                        isFirstBlock = false;
                        byte numBlocks = data[ptr++];
                        ptr += 2; // skip CRC
                        Console.WriteLine($"QDF Block Info: numBlocks={numBlocks}");
                        System.IO.File.Delete("qdf_blocks.txt");
                        continue;
                    }

                    byte blockType = data[ptr];
                    ptr++;
                    ushort blockSize = (ushort)(data[ptr] | (data[ptr + 1] << 8));
                    ptr += 2;
                    
                    System.IO.File.AppendAllText("qdf_blocks.txt", $"RAW BLOCK: Type={blockType:X2}, Size={blockSize}\n");

                    if (blockType == 0x00) // Header Block
                    {
                        if (ptr + blockSize > data.Length) break;

                        byte fileAttr = data[ptr];
                        byte[] nameBytes = new byte[17];
                        Array.Copy(data, ptr + 1, nameBytes, 0, 17);
                        string fileName = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\r', '\0', ' ');

                        ushort loadAddr = (ushort)(data[ptr + 18] | (data[ptr + 19] << 8));
                        ushort fileSize = (ushort)(data[ptr + 20] | (data[ptr + 21] << 8));
                        ushort execAddr = (ushort)(data[ptr + 22] | (data[ptr + 23] << 8));
                        
                        string logMsg = $"[QDF BLOCK] Name: {fileName}, Type: {blockType:X2}, Load: {loadAddr:X4}, Size: {fileSize}, Exec: {execAddr:X4}\n";
                        Console.WriteLine(logMsg);
                        System.IO.File.AppendAllText("qdf_blocks.txt", logMsg);

                        currentLoadAddr = loadAddr;
                        currentFileSize = fileSize;

                        if (execAddr != 0 && lastExecAddr == 0) // Only capture the FIRST valid execAddr (IPL/Entry point)
                        {
                            lastExecAddr = execAddr;
                        }

                        ptr += blockSize;
                        ptr += 2; // skip CRC
                    }
                    else if (blockType > 0x00) // Data Block
                    {
                        if (ptr + blockSize <= data.Length)
                        {
                            int loadLen = Math.Min((int)currentFileSize, (int)blockSize);
                            if (loadLen == 0) loadLen = blockSize;

                            byte[] fileData = new byte[loadLen];
                            Array.Copy(data, ptr, fileData, 0, loadLen);

                            Memory.LoadBinary(currentLoadAddr, fileData);
                            Console.WriteLine($"QDF Data: Loaded {fileData.Length} bytes at 0x{currentLoadAddr:X4}");
                            loadedAny = true;

                            currentLoadAddr += (ushort)loadLen;
                            if (currentFileSize >= loadLen) currentFileSize -= (ushort)loadLen;

                            ptr += blockSize;
                            ptr += 2; // skip CRC
                        }
                        else
                        {
                            break;
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
                    // Standard MZ-1500 memory mapping: Mon Low enabled, Mon High enabled, PCG disabled
                    Memory.WriteIo(0xE4, 0);

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

        public class Z80PioInterruptDevice : IIoDevice
        {
            public byte VectorA { get; set; } = 0xFF;
            public byte VectorB { get; set; } = 0xFF;
            public byte ModeA { get; set; } = 0;
            public byte ModeB { get; set; } = 0;
            public bool IntControlA { get; set; } = false;
            public bool IntControlB { get; set; } = false;

            public void Reset()
            {
                VectorA = 0xFF;
                VectorB = 0xFF;
            }

            public byte ReadIo(byte port) => 0xFF;

            public void WriteIo(byte port, byte data)
            {
                if (port == 0xFC)
                {
                    if ((data & 0x01) == 0)
                    {
                        VectorA = data;
                    }
                    else if ((data & 0x0F) == 0x0F)
                    {
                        ModeA = (byte)(data >> 6);
                    }
                    else if ((data & 0x0F) == 0x07)
                    {
                        IntControlA = (data & 0x80) != 0;
                    }
                }
                else if (port == 0xFD)
                {
                    if ((data & 0x01) == 0)
                    {
                        VectorB = data;
                    }
                    else if ((data & 0x0F) == 0x0F)
                    {
                        ModeB = (byte)(data >> 6);
                    }
                    else if ((data & 0x0F) == 0x07)
                    {
                        IntControlB = (data & 0x80) != 0;
                    }
                }
            }
        }

        private class TimerInterruptSource : IZ80InterruptSource
        {
            private readonly Z80PioInterruptDevice _pioInt;
            public TimerInterruptSource(Z80PioInterruptDevice pioInt)
            {
                _pioInt = pioInt;
            }

            public bool IntLineIsActive => _pendingA || _pendingB;
            
            public byte? ValueOnDataBus
            {
                get
                {
                    if (_pendingA) return _pioInt.VectorA;
                    if (_pendingB) return _pioInt.VectorB;
                    return 0xFF;
                }
            }
            
            public event EventHandler? NmiInterruptPulse;

            private bool _pendingA = false;
            private bool _pendingB = false;

            public void FireA()
            {
                _pendingA = true;
            }

            public void FireB()
            {
                _pendingB = true;
            }

            public void InterruptAcknowledge()
            {
                if (_pendingA)
                {
                    _pendingA = false;
                }
                else if (_pendingB)
                {
                    _pendingB = false;
                }
            }
        }

        private ulong _lastIntTStates = 0;
        private ulong _lastTempoTStates = 0;
        private ulong _lastBlinkTStates = 0;
        private ulong _lastPitCh0TStates = 0;
        private ulong _lastPitCh1TStates = 0;

        private readonly System.Diagnostics.Stopwatch _stopwatch = new();
        private volatile bool _stopRequested = false;

        public void Stop()
        {
            _stopRequested = true;
        }

        // Main CPU Run Loop - uses batch instruction execution instead of Cpu.Continue()
        // to avoid blocking the thread pool and starving the UI thread.
        public void Run()
        {
            Cpu.ClockFrequencyInMHz = 4.0m;

            _intSource = new TimerInterruptSource(_pioInt);
            Cpu.RegisterInterruptSource(_intSource);
            _stopwatch.Restart();

            int traceSize = 10000;
            string[] traceBuffer = new string[traceSize];
            int traceIndex = 0;
            ulong totalInstructions = 0;

            while (!_stopRequested)
            {
                // Execute a batch of instructions (roughly 1 frame worth = ~66666 T-states at 4MHz)
                ulong batchEnd = Cpu.TStatesElapsedSinceStart + 66666;

                while (Cpu.TStatesElapsedSinceStart < batchEnd && !_stopRequested)
                {
                    ushort pc = Cpu.Registers.PC;
                    byte b1 = Memory[pc];
                    byte b2 = Memory[(ushort)(pc + 1)];
                    byte b3 = Memory[(ushort)(pc + 2)];
                    traceBuffer[traceIndex] = $"PC: {pc:X4} | {b1:X2} {b2:X2} {b3:X2}";
                    traceIndex = (traceIndex + 1) % traceSize;
                    totalInstructions++;

                    if (pc == 0x0000 && Cpu.TStatesElapsedSinceStart > 100000)
                    {
                        Console.WriteLine("CRASH DETECTED! Jumped to 0000H. Stopping...");
                        _stopRequested = true;
                    }

                    Cpu.ExecuteNextInstruction();

                    ulong currentTStates = Cpu.TStatesElapsedSinceStart;

                    // PIT Channel 0 (894.886kHz -> every ~4 T-states at 4.0MHz)
                    if (currentTStates - _lastPitCh0TStates >= 4)
                    {
                        _lastPitCh0TStates = currentTStates;
                        _pit.TickChannel0();
                    }

                    // PIT Channel 1 (BLANK 15.7kHz -> every ~255 T-states at 4.0MHz)
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

                    // VBLANK detect (within frame)
                    if (currentTStates - _lastIntTStates >= 10000 && currentTStates - _lastIntTStates < 66666)
                    {
                        _vblank = false;
                    }
                }

                // End of frame: fire VBLANK interrupt
                _vblank = true;
                _lastIntTStates = Cpu.TStatesElapsedSinceStart;
                _intSource?.FireB();

                // Real-time synchronization: sleep to match 60 FPS
                long targetMs = (long)(Cpu.TStatesElapsedSinceStart / 4000.0);
                long actualMs = _stopwatch.ElapsedMilliseconds;
                int sleepMs = (int)(targetMs - actualMs);
                if (sleepMs > 1)
                {
                    System.Threading.Thread.Sleep(Math.Min(sleepMs, 50));
                }
                else
                {
                    // Even if we're behind, yield the thread briefly
                    System.Threading.Thread.Sleep(0);
                }
            }

            try
            {
                using var writer = new System.IO.StreamWriter("cpu_trace_last10k.txt");
                writer.WriteLine($"Total Instructions Executed: {totalInstructions}");
                writer.WriteLine("--- Last 10,000 Instructions ---");
                int count = (int)Math.Min(totalInstructions, (ulong)traceSize);
                int startIdx = totalInstructions < (ulong)traceSize ? 0 : traceIndex;
                for (int i = 0; i < count; i++)
                {
                    int idx = (startIdx + i) % traceSize;
                    writer.WriteLine(traceBuffer[idx]);
                }
            }
            catch { }
        }
    }
}
