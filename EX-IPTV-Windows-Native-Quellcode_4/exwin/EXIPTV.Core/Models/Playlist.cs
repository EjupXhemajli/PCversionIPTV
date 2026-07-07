using System.Collections.Generic;

namespace EXIPTV.Models;

public enum PlaylistType { Xtream, M3u }

/// <summary>Eine Quelle (Xtream-Panel oder M3U-Link) inkl. Zugangsdaten.</summary>
public class Playlist
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public PlaylistType Type { get; set; } = PlaylistType.Xtream;

    // Xtream
    public string Server { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    // M3U
    public string Url { get; set; } = "";

    public string UserAgent { get; set; } = "";
    public bool Enabled { get; set; } = true;

    // Laufzeit-Status (nicht Teil der Identität)
    public string Status { get; set; } = "neu";     // neu | lädt | ok | fehler
    public string LastError { get; set; } = "";
    public int LiveCount { get; set; }
    public int MovieCount { get; set; }
    public int SeriesCount { get; set; }
}

/// <summary>Persistenter App-Zustand.</summary>
public class AppState
{
    public List<Playlist> Playlists { get; set; } = new();
    public HashSet<string> Favorites { get; set; } = new();
    public int BufferMs { get; set; } = 2000;       // LibVLC network-caching
    public double Volume { get; set; } = 90;
    public string LastChannelId { get; set; } = "";
}
