namespace EXIPTV.Models;

public enum StreamKind { Live, Movie, Series }

/// <summary>Ein Sender, Film oder eine Serie.</summary>
public class Channel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Logo { get; set; } = "";
    public string Group { get; set; } = "";
    public string Url { get; set; } = "";
    public StreamKind Kind { get; set; } = StreamKind.Live;
    public long Added { get; set; }          // Xtream "added" (Unix) – für Neuheits-Sortierung
    public string Year { get; set; } = "";
    public string SeriesId { get; set; } = ""; // nur bei Serien
    public string ContainerExt { get; set; } = "";
}
