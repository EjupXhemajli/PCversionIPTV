using System;
using LibVLCSharp.Shared;

namespace EXIPTV.Player;

/// <summary>
/// Kapselt LibVLC. Die VLC-Engine übernimmt Demuxing, Decoding (hardware-
/// beschleunigt) und Pufferung nativ – deutlich stabiler als Browser-MSE.
/// </summary>
public sealed class VlcPlayer : IDisposable
{
    public LibVLC LibVLC { get; }
    public MediaPlayer MediaPlayer { get; }

    private int _networkCachingMs = 2000;
    public event Action<string>? StatusChanged;

    public VlcPlayer()
    {
        // LibVLC-Native-Bibliotheken laden. Bei einem Ordner-Publish liegen sie
        // unter <App>\libvlc\win-x64\ – diesen Pfad explizit angeben, sonst
        // scheitert die Initialisierung je nach Umgebung.
        try
        {
            var libDir = System.IO.Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");
            if (System.IO.Directory.Exists(libDir))
                Core.Initialize(libDir);
            else
                Core.Initialize();
        }
        catch
        {
            Core.Initialize(); // letzter Versuch mit Standard-Suche
        }
        // Globale, konservative Optionen für stabiles Live-Streaming.
        LibVLC = new LibVLC(
            "--network-caching=2000",
            "--live-caching=2000",
            "--file-caching=2000",
            "--clock-jitter=0",
            "--clock-synchro=0",
            "--http-reconnect",
            "--adaptive-logic=highest",
            "--no-video-title-show",
            "--quiet"
        );
        MediaPlayer = new MediaPlayer(LibVLC) { EnableHardwareDecoding = true };

        MediaPlayer.EncounteredError += (_, __) => StatusChanged?.Invoke("Fehler");
        MediaPlayer.Buffering += (_, e) =>
            StatusChanged?.Invoke(e.Cache >= 100 ? "" : $"Puffern {e.Cache:0}%");
        MediaPlayer.Playing += (_, __) => StatusChanged?.Invoke("");
        MediaPlayer.EndReached += (_, __) => StatusChanged?.Invoke("Ende");
    }

    public void SetNetworkCaching(int ms)
    {
        _networkCachingMs = Math.Clamp(ms, 500, 10000);
    }

    public void Play(string url, string userAgent = "")
    {
        Stop();
        var media = new Media(LibVLC, new Uri(url));
        // Pro-Stream-Optionen (überschreiben die globalen für diesen Stream).
        media.AddOption($":network-caching={_networkCachingMs}");
        media.AddOption($":live-caching={_networkCachingMs}");
        media.AddOption(":http-reconnect");
        media.AddOption(":adaptive-logic=highest");
        if (!string.IsNullOrWhiteSpace(userAgent))
            media.AddOption($":http-user-agent={userAgent}");
        MediaPlayer.Play(media);
        media.Dispose();
    }

    public void Stop()
    {
        if (MediaPlayer.IsPlaying || MediaPlayer.State == VLCState.Paused)
            MediaPlayer.Stop();
    }

    public void TogglePause()
    {
        if (MediaPlayer.CanPause) MediaPlayer.Pause();
    }

    public int Volume
    {
        get => MediaPlayer.Volume;
        set => MediaPlayer.Volume = Math.Clamp(value, 0, 100);
    }

    public void Dispose()
    {
        MediaPlayer.Dispose();
        LibVLC.Dispose();
    }
}
