using Avalonia;
using System;

namespace Mz1500SoundPlayer;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "headless")
        {
            var machine = new Sound.Emulator.Mz1500Machine();
            machine.Memory.LoadRom(0x0000, System.IO.File.ReadAllBytes(@"Mz1500SoundPlayer\romsample\IPL.ROM"));
            if (System.IO.File.Exists(@"Mz1500SoundPlayer\romsample\EXT.ROM"))
                machine.Memory.LoadRom(0xE800, System.IO.File.ReadAllBytes(@"Mz1500SoundPlayer\romsample\EXT.ROM"));
            machine.LoadMzt("test_rhythm.mzt");
            
            for (int i = 0; i < 500000; i++)
            {
                machine.Cpu.ExecuteNextInstruction();
            }
            Console.WriteLine("Headless run complete.");
            return;
        }
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
