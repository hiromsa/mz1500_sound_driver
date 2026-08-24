using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Mz1500SoundPlayer
{
    public partial class EmulatorWindow : Window
    {
        private WriteableBitmap _bitmap;
        private Sound.Emulator.Mz1500Machine? _machine;
        private Thread? _renderThread;
        private bool _isRunning = false;

        public EmulatorWindow()
        {
            InitializeComponent();
            _bitmap = new WriteableBitmap(
                new PixelSize(320, 200),
                new Vector(96, 96),
                PixelFormat.Bgra8888);
            
            var screenImage = this.FindControl<Image>("ScreenImage");
            if (screenImage != null)
            {
                screenImage.Source = _bitmap;
            }

            this.Closed += EmulatorWindow_Closed;
        }

        public void Start(Sound.Emulator.Mz1500Machine machine)
        {
            _machine = machine;
            _isRunning = true;
            _renderThread = new Thread(RenderLoop)
            {
                IsBackground = true
            };
            _renderThread.Start();
        }

        private void EmulatorWindow_Closed(object? sender, EventArgs e)
        {
            _isRunning = false;
        }

        private void RenderLoop()
        {
            // Standard MZ PC palette (BGRA format: 0xAARRGGBB in Little-Endian)
            // 0: Black, 1: Blue, 2: Red, 3: Magenta, 4: Green, 5: Cyan, 6: Yellow, 7: White
            uint[] pcPalette = new uint[8]
            {
                0xFF000000, // 0: Black
                0xFF0000FF, // 1: Blue
                0xFFFF0000, // 2: Red
                0xFFFF00FF, // 3: Magenta
                0xFF00FF00, // 4: Green
                0xFF00FFFF, // 5: Cyan
                0xFFFFFF00, // 6: Yellow
                0xFFFFFFFF  // 7: White
            };

            int width = 320;
            int height = 200;
            uint[] buffer = new uint[width * height];

            while (_isRunning)
            {
                if (_machine != null)
                {
                    var memory = _machine.Memory;
                    var vram = memory.GetVram();
                    var cgrom = memory.CgRom;
                    var hwPalette = memory.Palette;

                    if (cgrom != null && cgrom.Length >= 0x1000)
                    {
                        for (int y = 0; y < 25; y++)
                        {
                            for (int x = 0; x < 40; x++)
                            {
                                int charIndex = y * 40 + x;
                                byte charCode = vram[charIndex];
                                byte attr = vram[0x800 + charIndex];

                                // MZ-1500/700 attribute byte:
                                // Bits 4-6: Foreground color index
                                // Bits 0-2: Background color index
                                int fgColor = hwPalette[(attr >> 4) & 0x07];
                                int bgColor = hwPalette[attr & 0x07];

                                uint fg = pcPalette[fgColor & 7];
                                uint bg = pcPalette[bgColor & 7];

                                // Bit 7 of attr selects upper 256 characters in CGROM
                                int fontOffset = (charCode << 3) | ((attr & 0x80) << 4);

                                for (int py = 0; py < 8; py++)
                                {
                                    byte pattern = (fontOffset + py < cgrom.Length) ? cgrom[fontOffset + py] : (byte)0;
                                    for (int px = 0; px < 8; px++)
                                    {
                                        bool pixel = (pattern & (0x80 >> px)) != 0;
                                        int pxX = x * 8 + px;
                                        int pxY = y * 8 + py;
                                        buffer[pxY * width + pxX] = pixel ? fg : bg;
                                    }
                                }
                            }
                        }
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (_bitmap != null)
                    {
                        using var locked = _bitmap.Lock();
                        Marshal.Copy((int[])(object)buffer, 0, locked.Address, buffer.Length);
                    }
                });

                Thread.Sleep(16); // ~60 FPS
            }
        }
    }
}
