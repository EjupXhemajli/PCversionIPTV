using System;
using System.IO;
using System.Text.Json;
using EXIPTV.Models;

namespace EXIPTV.Services;

/// <summary>Speichert den App-Zustand (inkl. Zugangsdaten) als JSON unter %APPDATA%\EXIPTV.</summary>
public static class Storage
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static string Dir
    {
        get
        {
            var d = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EXIPTV");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    public static string StatePath => Path.Combine(Dir, "state.json");

    public static AppState Load()
    {
        try
        {
            if (!File.Exists(StatePath)) return new AppState();
            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<AppState>(json, Opts) ?? new AppState();
        }
        catch { return new AppState(); }
    }

    public static void Save(AppState state)
    {
        try
        {
            // Laufzeit-Status nicht mitspeichern (wird beim Start neu ermittelt).
            foreach (var p in state.Playlists) { p.Status = "neu"; p.LastError = ""; }
            File.WriteAllText(StatePath, JsonSerializer.Serialize(state, Opts));
        }
        catch { /* ignore */ }
    }
}
