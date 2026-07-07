using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace EXIPTV;

public partial class App : Application
{
    public App()
    {
        // Jeden unbehandelten Fehler protokollieren und anzeigen, statt still
        // abzustürzen. So ist ein Startproblem nachvollziehbar.
        DispatcherUnhandledException += OnDispatcherError;
        AppDomain.CurrentDomain.UnhandledException += OnDomainError;
    }

    private static string LogPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EXIPTV");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "error.log");
        }
    }

    private static void Log(Exception ex, string quelle)
    {
        try
        {
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {quelle}: {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* ignore */ }
    }

    private void OnDispatcherError(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception, "UI");
        MessageBox.Show(
            "EX-IPTV ist auf einen Fehler gestoßen:\n\n" + e.Exception.Message +
            "\n\nDetails wurden protokolliert:\n" + LogPath,
            "EX-IPTV", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // App am Leben halten, wenn möglich
    }

    private void OnDomainError(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log(ex, "Domain");
            MessageBox.Show(
                "EX-IPTV muss beendet werden:\n\n" + ex.Message +
                "\n\nDetails:\n" + LogPath,
                "EX-IPTV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
