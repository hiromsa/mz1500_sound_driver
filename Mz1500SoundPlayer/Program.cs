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
        // レガシーエンコーディング(Shift-JIS)をサポートするためにコードページプロバイダを登録
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
