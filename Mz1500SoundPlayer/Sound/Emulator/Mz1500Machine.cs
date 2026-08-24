using System;
using Konamiman.Z80dotNet;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class Mz1500Machine
    {
        public Z80Processor Cpu { get; private set; }
        public Mz1500Memory Memory { get; private set; }
        public IoController Io { get; private set; }

        public Mz1500Machine()
        {
            Cpu = new Z80Processor();
            Memory = new Mz1500Memory();
            Io = new IoController();

            Cpu.Memory = Memory;
            Cpu.PortsSpace = Io;
            
            // Try loading MONITOR ROM
            LoadRom();
            
            // Setup Devices
            var pit = new Intel8253();
            var pio = new Intel8255();
            var psgL = new Sn76489an();
            var psgR = new Sn76489an();
            var opm = new YM2151Core();
            opm.Start(0, 44100, 4000000);
            
            var keyboard = new Keyboard();
            var beep = new Beep();
            var pcg = new Pcg();

            // Connect 8255 to Keyboard and Beep
            pio.OnPortARead = () => keyboard.ReadMatrix();
            pio.OnPortCWrite = (data) => {
                // Beep switch, motor switch, etc.
                // Simplified beep control
                beep.SetOn((data & 0x01) != 0); // Example, need precise MZ-1500 bit mapping
            };

            // Memory mapped I/O: 8253 is at E004-E007, 8255 is at E000-E003
            // In Z80dotNet, we can just intercept memory writes via our Mz1500Memory,
            // but currently Mz1500Memory just writes to RAM. We should route E000-E00F to devices.
            Memory.SetDeviceMapper((addr, data) => 
            {
                if (addr >= 0xE000 && addr <= 0xE003) pio.WriteIo((byte)(addr & 3), data);
                else if (addr >= 0xE004 && addr <= 0xE007) pit.WriteIo((byte)(addr & 3), data);
            }, 
            (addr) => 
            {
                if (addr >= 0xE000 && addr <= 0xE003) return pio.ReadIo((byte)(addr & 3));
                else if (addr >= 0xE004 && addr <= 0xE007) return pit.ReadIo((byte)(addr & 3));
                return 0xFF;
            });

            // Standard I/O (IN/OUT)
            // PSG
            Io.RegisterDevice(0xF2, psgL);
            Io.RegisterDevice(0xF3, psgR);

            // YM2151 (uses 8-bit port addressing in IoController, but YM2151 uses 0x0708/0x0709)
            // We'll wrap it as an IIoDevice for the IoController which only matches lower 8 bits.
            // Port 0x08 and 0x09
            Io.RegisterDevice(0x08, new Ym2151Wrapper(opm, 0)); // Address
            Io.RegisterDevice(0x09, new Ym2151Wrapper(opm, 1)); // Data
        }
        
        // Wrapper for YM2151 to fit IIoDevice
        private class Ym2151Wrapper : IIoDevice
        {
            private YM2151Core _core;
            private int _type; // 0=Addr, 1=Data
            public Ym2151Wrapper(YM2151Core core, int type) { _core = core; _type = type; }
            public void Reset() { }
            public byte ReadIo(byte port) => 0; // Reading not implemented yet
            public void WriteIo(byte port, byte data) {
                // Port parameter: 0 = address register, 1 = data register.
                _core.Write(0, _type, 0, data); 
            }
        }

        private void LoadRom()
        {
            try
            {
                // Typical ROM names for MZ-1500
                string romPath = "mz1500.rom";
                if (!System.IO.File.Exists(romPath))
                    romPath = "mz1500.ipl";
                
                if (System.IO.File.Exists(romPath))
                {
                    byte[] romData = System.IO.File.ReadAllBytes(romPath);
                    Memory.LoadBinary(0x0000, romData);
                    Console.WriteLine($"Loaded Monitor ROM: {romPath}");
                }
                else
                {
                    Console.WriteLine("Monitor ROM not found. Running without ROM.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load Monitor ROM: {ex.Message}");
            }
        }

        public void Reset()
        {
            Cpu.Reset();
            // TODO: Reset all registered IO/Memory devices
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
                
                // Set PC to the execution address if we are ready to run
                Cpu.Registers.PC = execAddr;
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load MZT: {ex.Message}");
                return false;
            }
        }

        // Executes until a HALT instruction or break condition is met
        public void Run()
        {
            // Set some execution limits or breakpoints later for debugging
            Cpu.Continue();
        }
    }
}

