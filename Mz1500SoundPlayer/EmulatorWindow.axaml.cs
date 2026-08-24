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
        private Image? _screenImage;
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
            
            _screenImage = this.FindControl<Image>("ScreenImage");
            if (_screenImage != null)
            {
                _screenImage.Source = _bitmap;
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
                    var pcg = memory.GetPcg();
                    var hwPalette = memory.Palette;
                    byte priority = memory.Priority;

                    if (cgrom != null && cgrom.Length >= 0x1000)
                    {
                        for (int y = 0; y < 25; y++)
                        {
                            for (int x = 0; x < 40; x++)
                            {
                                int charIndex = y * 40 + x;
                                byte textCode = vram[charIndex];
                                byte textAttr = vram[0x800 + charIndex];
                                byte pcgCode = vram[0x400 + charIndex];
                                byte pcgAttr = vram[0xC00 + charIndex];

                                // MZ-1500/700 attribute byte:
                                // Bits 4-6: Foreground color index
                                // Bits 0-2: Background color index
                                int fgColor = hwPalette[(textAttr >> 4) & 0x07];
                                int bgColor = hwPalette[textAttr & 0x07];

                                uint fg = pcPalette[fgColor & 7];
                                uint bg = pcPalette[bgColor & 7];

                                // Bit 7 of textAttr selects upper 256 characters in CGROM
                                int fontOffset = (textCode << 3) | ((textAttr & 0x80) << 4);

                                // PCG code offset (1024 characters * 8 bytes):
                                int pcgOffset = (pcgCode << 3) | ((pcgAttr & 0xC0) << 5);
                                bool pcgActive = (priority & 1) != 0 && (pcgAttr & 8) != 0 && pcg != null && pcg.Length >= 0x6000;

                                for (int py = 0; py < 8; py++)
                                {
                                    byte patT = (fontOffset + py < cgrom.Length) ? cgrom[fontOffset + py] : (byte)0;

                                    byte patB = 0, patR = 0, patG = 0;
                                    if (pcgActive && pcgOffset + py < 0x2000)
                                    {
                                        patB = pcg[pcgOffset + py + 0x0000];
                                        patR = pcg[pcgOffset + py + 0x2000];
                                        patG = pcg[pcgOffset + py + 0x4000];
                                    }

                                    for (int px = 0; px < 8; px++)
                                    {
                                        int bitMask = 0x80 >> px;
                                        bool textPixel = (patT & bitMask) != 0;

                                        int pxX = x * 8 + px;
                                        int pxY = y * 8 + py;
                                        int bufIdx = pxY * width + pxX;

                                        if (pcgActive)
                                        {
                                            int b = (patB & bitMask) != 0 ? 1 : 0;
                                            int r = (patR & bitMask) != 0 ? 2 : 0;
                                            int g = (patG & bitMask) != 0 ? 4 : 0;
                                            int pcgDot = hwPalette[(b | r | g) & 7];

                                            if ((priority & 2) != 0)
                                            {
                                                // PCG > Text
                                                if (pcgDot != 0)
                                                    buffer[bufIdx] = pcPalette[pcgDot & 7];
                                                else
                                                    buffer[bufIdx] = textPixel ? fg : bg;
                                            }
                                            else
                                            {
                                                // Text FG > PCG > Text BG
                                                if (textPixel)
                                                    buffer[bufIdx] = fg;
                                                else if (pcgDot != 0)
                                                    buffer[bufIdx] = pcPalette[pcgDot & 7];
                                                else
                                                    buffer[bufIdx] = bg;
                                            }
                                        }
                                        else
                                        {
                                            buffer[bufIdx] = textPixel ? fg : bg;
                                        }
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
                        _screenImage?.InvalidateVisual();
                    }
                });

                Thread.Sleep(16); // ~60 FPS
            }
        }
    }
}
