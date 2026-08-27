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
        private bool _isRunning = false;

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            Sound.Emulator.EmulatorLogger.Instance.Log("UI", "EMULATOR WINDOW OPENED!");
            Sound.Emulator.EmulatorLogger.Instance.Log("UI", "この状態でキーボードの M や Q などを押してください。");
            this.Focus(); // Ensure window has focus for key events
        }

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
            this.KeyDown += EmulatorWindow_KeyDown;
            this.KeyUp += EmulatorWindow_KeyUp;
        }

        private void EmulatorWindow_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            Sound.Emulator.EmulatorLogger.Instance.Log("UI", $"KeyDown: {e.Key}");
            if (_machine == null) return;
            var pos = MapKeyToMatrix(e.Key);
            if (pos.HasValue)
            {
                Sound.Emulator.EmulatorLogger.Instance.Log("UI", $"Mapped to Matrix: row={pos.Value.row}, col={pos.Value.col}");
                _machine.Keyboard.SetKeyDown(pos.Value.row, pos.Value.col);
            }
            else
            {
                Sound.Emulator.EmulatorLogger.Instance.Log("UI", $"Key not mapped.");
            }
        }

        private void EmulatorWindow_KeyUp(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            Sound.Emulator.EmulatorLogger.Instance.Log("UI", $"KeyUp: {e.Key}");
            if (_machine == null) return;
            var pos = MapKeyToMatrix(e.Key);
            if (pos.HasValue)
            {
                _machine.Keyboard.SetKeyUp(pos.Value.row, pos.Value.col);
            }
        }

        private static (int row, int col)? MapKeyToMatrix(Avalonia.Input.Key key)
        {
            return key switch
            {
                // Row 0
                Avalonia.Input.Key.Enter or Avalonia.Input.Key.Return => (0, 0),
                Avalonia.Input.Key.Oem1 => (0, 1), // :*
                Avalonia.Input.Key.OemPlus or Avalonia.Input.Key.Add => (0, 2), // ;+
                Avalonia.Input.Key.Tab => (0, 4),
                Avalonia.Input.Key.F9 => (0, 5),
                Avalonia.Input.Key.PageUp => (0, 6),
                Avalonia.Input.Key.PageDown => (0, 7),

                // Row 1
                Avalonia.Input.Key.Oem6 or Avalonia.Input.Key.OemCloseBrackets => (1, 3), // ]}
                Avalonia.Input.Key.Oem4 or Avalonia.Input.Key.OemOpenBrackets => (1, 4),  // [{
                Avalonia.Input.Key.Oem3 or Avalonia.Input.Key.OemTilde => (1, 5),        // @`
                Avalonia.Input.Key.Z => (1, 6),
                Avalonia.Input.Key.Y => (1, 7),

                // Row 2
                Avalonia.Input.Key.X => (2, 0),
                Avalonia.Input.Key.W => (2, 1),
                Avalonia.Input.Key.V => (2, 2),
                Avalonia.Input.Key.U => (2, 3),
                Avalonia.Input.Key.T => (2, 4),
                Avalonia.Input.Key.S => (2, 5),
                Avalonia.Input.Key.R => (2, 6),
                Avalonia.Input.Key.Q => (2, 7),

                // Row 3
                Avalonia.Input.Key.P => (3, 0),
                Avalonia.Input.Key.O => (3, 1),
                Avalonia.Input.Key.N => (3, 2),
                Avalonia.Input.Key.M => (3, 3),
                Avalonia.Input.Key.L => (3, 4),
                Avalonia.Input.Key.K => (3, 5),
                Avalonia.Input.Key.J => (3, 6),
                Avalonia.Input.Key.I => (3, 7),

                // Row 4
                Avalonia.Input.Key.H => (4, 0),
                Avalonia.Input.Key.G => (4, 1),
                Avalonia.Input.Key.F => (4, 2),
                Avalonia.Input.Key.E => (4, 3),
                Avalonia.Input.Key.D => (4, 4),
                Avalonia.Input.Key.C => (4, 5),
                Avalonia.Input.Key.B => (4, 6),
                Avalonia.Input.Key.A => (4, 7),

                // Row 5
                Avalonia.Input.Key.D8 or Avalonia.Input.Key.NumPad8 => (5, 0),
                Avalonia.Input.Key.D7 or Avalonia.Input.Key.NumPad7 => (5, 1),
                Avalonia.Input.Key.D6 or Avalonia.Input.Key.NumPad6 => (5, 2),
                Avalonia.Input.Key.D5 or Avalonia.Input.Key.NumPad5 => (5, 3),
                Avalonia.Input.Key.D4 or Avalonia.Input.Key.NumPad4 => (5, 4),
                Avalonia.Input.Key.D3 or Avalonia.Input.Key.NumPad3 => (5, 5),
                Avalonia.Input.Key.D2 or Avalonia.Input.Key.NumPad2 => (5, 6),
                Avalonia.Input.Key.D1 or Avalonia.Input.Key.NumPad1 => (5, 7),

                // Row 6
                Avalonia.Input.Key.OemPeriod or Avalonia.Input.Key.Decimal => (6, 0),
                Avalonia.Input.Key.OemComma => (6, 1),
                Avalonia.Input.Key.D9 or Avalonia.Input.Key.NumPad9 => (6, 2),
                Avalonia.Input.Key.D0 or Avalonia.Input.Key.NumPad0 => (6, 3),
                Avalonia.Input.Key.Space => (6, 4),
                Avalonia.Input.Key.OemMinus or Avalonia.Input.Key.Subtract => (6, 5),
                Avalonia.Input.Key.Oem7 => (6, 6),
                Avalonia.Input.Key.Oem5 or Avalonia.Input.Key.OemPipe => (6, 7),

                // Row 7
                Avalonia.Input.Key.Oem2 or Avalonia.Input.Key.Divide => (7, 0),
                Avalonia.Input.Key.Oem102 or Avalonia.Input.Key.OemBackslash => (7, 1),
                Avalonia.Input.Key.Left => (7, 2),
                Avalonia.Input.Key.Right => (7, 3),
                Avalonia.Input.Key.Down => (7, 4),
                Avalonia.Input.Key.Up => (7, 5),
                Avalonia.Input.Key.Delete => (7, 6),
                Avalonia.Input.Key.Insert => (7, 7),

                // Row 8
                Avalonia.Input.Key.LeftShift or Avalonia.Input.Key.RightShift => (8, 0),
                Avalonia.Input.Key.LeftCtrl or Avalonia.Input.Key.RightCtrl => (8, 6),
                Avalonia.Input.Key.Back => (8, 7),

                // Row 9
                Avalonia.Input.Key.F5 => (9, 3),
                Avalonia.Input.Key.F4 => (9, 4),
                Avalonia.Input.Key.F3 => (9, 5),
                Avalonia.Input.Key.F2 => (9, 6),
                Avalonia.Input.Key.F1 => (9, 7),

                _ => null
            };
        }

        private NAudio.Wave.WasapiOut? _audioOut;
        private Sound.Emulator.EmulatorAudioProvider? _audioProvider;
        private Avalonia.Threading.DispatcherTimer? _renderTimer;

        private static readonly int[] PcPalette = new int[8]
        {
            unchecked((int)0xFF000000), // 0: Black
            unchecked((int)0xFF0000FF), // 1: Blue
            unchecked((int)0xFFFF0000), // 2: Red
            unchecked((int)0xFFFF00FF), // 3: Magenta
            unchecked((int)0xFF00FF00), // 4: Green
            unchecked((int)0xFF00FFFF), // 5: Cyan
            unchecked((int)0xFFFFFF00), // 6: Yellow
            unchecked((int)0xFFFFFFFF)  // 7: White
        };

        private readonly int[] _frameBuffer = new int[320 * 200];

        public void Start(Sound.Emulator.Mz1500Machine machine)
        {
            _machine = machine;
            _isRunning = true;

            _renderTimer = new Avalonia.Threading.DispatcherTimer(
                TimeSpan.FromMilliseconds(16),
                Avalonia.Threading.DispatcherPriority.Render,
                (s, e) => RenderFrame());
            _renderTimer.Start();

            try
            {
                _audioProvider = new Sound.Emulator.EmulatorAudioProvider(_machine);
                _audioOut = new NAudio.Wave.WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 50);
                _audioOut.Init(_audioProvider);
                _audioOut.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start emulator audio: {ex.Message}");
            }
        }

        private void EmulatorWindow_Closed(object? sender, EventArgs e)
        {
            _isRunning = false;
            _renderTimer?.Stop();
            _renderTimer = null;
            _machine?.Stop();

            try
            {
                _audioOut?.Stop();
                _audioOut?.Dispose();
                _audioOut = null;
            }
            catch { }
        }

        private void RenderFrame()
        {
            if (!_isRunning || _machine == null || _bitmap == null) return;

            var memory = _machine.Memory;
            var vram = memory.GetVram();
            var cgrom = memory.CgRom;
            var pcg = memory.GetPcg();
            var hwPalette = memory.Palette;
            byte priority = memory.Priority;

            const int width = 320;
            const int height = 200;

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

                        int fg = PcPalette[fgColor & 7];
                        int bg = PcPalette[bgColor & 7];

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
                                            _frameBuffer[bufIdx] = PcPalette[pcgDot & 7];
                                        else
                                            _frameBuffer[bufIdx] = textPixel ? fg : bg;
                                    }
                                    else
                                    {
                                        // Text FG > PCG > Text BG
                                        if (textPixel)
                                            _frameBuffer[bufIdx] = fg;
                                        else if (pcgDot != 0)
                                            _frameBuffer[bufIdx] = PcPalette[pcgDot & 7];
                                        else
                                            _frameBuffer[bufIdx] = bg;
                                    }
                                }
                                else
                                {
                                    _frameBuffer[bufIdx] = textPixel ? fg : bg;
                                }
                            }
                        }
                    }
                }
            }

            using var locked = _bitmap.Lock();
            Marshal.Copy(_frameBuffer, 0, locked.Address, _frameBuffer.Length);
            _screenImage?.InvalidateVisual();
        }
    }
}
