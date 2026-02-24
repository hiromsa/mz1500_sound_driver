using Avalonia;
using System;
using System.Linq;

namespace Mz1500SoundPlayer;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // レガシーエンコーディング(Shift-JIS)をサポートするためにコードページプロバイダを登録
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        if (args.Length > 0 && args[0] == "test")
        {
            var player = new Mz1500SoundPlayer.Sound.MmlPlayerModel();
            string mml = @"@v1 = {15,14|13,11,8,7,5,3,2}
ABC @t1,83

A L o4@q1l8@v1 

A c+2 l4 g+ >c+ f+ g+ >c+ e<<";
            var log = player.ExportQdc(mml, "test.qdc");
            Console.WriteLine(log);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
