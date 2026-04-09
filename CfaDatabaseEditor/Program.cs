using Avalonia;
using System;
using System.Text;

namespace CfaDatabaseEditor;

sealed class Program
{
    public static StreamWriter? Log;

    [STAThread]
    public static void Main(string[] args)
    {
        Log = new StreamWriter("./editor.log", append: false) { AutoFlush = true };
        Log.WriteLine($"[{DateTime.Now:HH:mm:ss}] App starting...");

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Log.WriteLine($"[UNHANDLED] {e.ExceptionObject}");
            Log.Flush();
        };

        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Log.WriteLine($"[{DateTime.Now:HH:mm:ss}] Encoding registered, launching Avalonia...");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.WriteLine($"[FATAL] {ex}");
        }
        finally
        {
            Log.WriteLine($"[{DateTime.Now:HH:mm:ss}] App exiting");
            Log.Flush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
