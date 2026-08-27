using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Mz1500SoundPlayer.Sound.Emulator;
using System;
using System.Linq;

namespace Mz1500SoundPlayer
{
    public partial class DebuggerWindow : Window
    {
        private Mz1500Machine? _machine;
        private DispatcherTimer _timer;

        public DebuggerWindow()
        {
            InitializeComponent();
            
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _timer.Tick += Timer_Tick;
            
            EmulatorLogger.Instance.OnLogAdded += OnLogAdded;
        }

        public void SetMachine(Mz1500Machine machine)
        {
            _machine = machine;
            _timer.Start();
            UpdateLogList();
        }

        private void OnLogAdded(string entry)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var items = LstLogs.Items.Cast<string>().ToList();
                items.Add(entry);
                if (items.Count > 100)
                {
                    items.RemoveAt(0);
                }
                LstLogs.ItemsSource = items;
                if (items.Count > 0)
                {
                    LstLogs.ScrollIntoView(items.Last());
                }
            });
        }

        private void UpdateLogList()
        {
            var logs = EmulatorLogger.Instance.GetRecentLogs().TakeLast(100).ToList();
            LstLogs.ItemsSource = logs;
            if (logs.Count > 0)
            {
                LstLogs.ScrollIntoView(logs.Last());
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_machine == null) return;

            TxtStatus.Text = _machine.IsPaused ? "Status: PAUSED" : "Status: RUNNING";

            if (_machine.IsPaused)
            {
                var cpu = _machine.Cpu;
                var reg = cpu.Registers;
                TxtRegisters.Text = $"PC: {reg.PC:X4}\n" +
                                    $"SP: {reg.SP:X4}\n" +
                                    $"A: {reg.A:X2}  F: {reg.F:X2}\n" +
                                    $"BC: {reg.BC:X4}\n" +
                                    $"DE: {reg.DE:X4}\n" +
                                    $"HL: {reg.HL:X4}\n" +
                                    $"IX: {reg.IX:X4}\n" +
                                    $"IY: {reg.IY:X4}";
            }
            else
            {
                TxtRegisters.Text = "(Running...)";
            }
        }

        private void BtnPause_Click(object? sender, RoutedEventArgs e)
        {
            _machine?.Pause();
            Timer_Tick(null, EventArgs.Empty);
        }

        private void BtnResume_Click(object? sender, RoutedEventArgs e)
        {
            _machine?.Resume();
            Timer_Tick(null, EventArgs.Empty);
        }

        private void BtnStep_Click(object? sender, RoutedEventArgs e)
        {
            _machine?.Step();
            Timer_Tick(null, EventArgs.Empty);
        }

        private void BtnDump_Click(object? sender, RoutedEventArgs e)
        {
            if (_machine == null) return;
            if (ushort.TryParse(TxtDumpAddress.Text, System.Globalization.NumberStyles.HexNumber, null, out ushort addr))
            {
                var bytes = _machine.Memory.GetContents(addr, 256); // Read 256 bytes
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < bytes.Length; i += 16)
                {
                    sb.Append($"{addr + i:X4}: ");
                    for (int j = 0; j < 16; j++)
                    {
                        if (i + j < bytes.Length)
                            sb.Append($"{bytes[i + j]:X2} ");
                    }
                    sb.AppendLine();
                }
                TxtMemory.Text = sb.ToString();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _timer.Stop();
            EmulatorLogger.Instance.OnLogAdded -= OnLogAdded;
        }
    }
}
