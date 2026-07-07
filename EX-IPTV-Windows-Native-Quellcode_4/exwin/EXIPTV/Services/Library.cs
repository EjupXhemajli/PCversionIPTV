using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EXIPTV.Models;

namespace EXIPTV.Services;

/// <summary>Hält alle geladenen Kanäle und liefert gefilterte Sichten (Kategorien, Listen).</summary>
public class Library
{
    private readonly XtreamClient _xt;
    private readonly M3UParser _m3u;
    private readonly List<Channel> _all = new();
    private readonly object _lock = new();

    public AppState State { get; }

    public Library(AppState state)
    {
        State = state;
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        _xt = new XtreamClient(http);
        _m3u = new M3UParser(http);
    }

    private static readonly string[] AdultKeys =
        { "xxx", "adult", "porn", "erotic", "erotik", "18+", "for adults", "brazzers", "onlyfans" };

    private static bool IsAdult(Channel c)
    {
        var s = (c.Group + " " + c.Name).ToLowerInvariant();
        return AdultKeys.Any(k => s.Contains(k));
    }

    /// <summary>Lädt eine Playlist (im Hintergrund aufzurufen).</summary>
    public async Task LoadPlaylistAsync(Playlist p, CancellationToken ct = default)
    {
        p.Status = "lädt";
        p.LastError = "";
        try
        {
            List<Channel> loaded;
            if (p.Type == PlaylistType.Xtream)
            {
                var r = await _xt.LoadAsync(p, ct);
                if (r.Error != null && r.Channels.Count == 0)
                { p.Status = "fehler"; p.LastError = r.Error; return; }
                loaded = r.Channels;
                p.LiveCount = r.Live; p.MovieCount = r.Movies; p.SeriesCount = r.Series;
                if (r.Error != null) p.LastError = r.Error;
            }
            else
            {
                var (list, err) = await _m3u.LoadUrlAsync(p.Url, p.UserAgent, p.Id, ct);
                if (err != null) { p.Status = "fehler"; p.LastError = err; return; }
                loaded = list;
                p.LiveCount = list.Count(c => c.Kind == StreamKind.Live);
                p.MovieCount = list.Count(c => c.Kind == StreamKind.Movie);
                p.SeriesCount = list.Count(c => c.Kind == StreamKind.Series);
            }

            lock (_lock)
            {
                _all.RemoveAll(c => c.Id.StartsWith(p.Id + "_"));
                _all.AddRange(loaded.Where(c => !IsAdult(c)));
            }
            p.Status = "ok";
        }
        catch (OperationCanceledException) { p.Status = "abgebrochen"; }
        catch (Exception ex) { p.Status = "fehler"; p.LastError = ex.Message; }
    }

    public void RemovePlaylistChannels(string plId)
    {
        lock (_lock) _all.RemoveAll(c => c.Id.StartsWith(plId + "_"));
    }

    // ---- Sichten ----

    public record Category(string Name, int Count);

    public List<Category> Categories(StreamKind kind)
    {
        lock (_lock)
        {
            return _all.Where(c => c.Kind == kind)
                       .GroupBy(c => c.Group)
                       .Select(g => new Category(g.Key, g.Count()))
                       .OrderBy(c => c.Name)
                       .ToList();
        }
    }

    public List<Channel> InCategory(StreamKind kind, string group)
    {
        lock (_lock)
            return _all.Where(c => c.Kind == kind && c.Group == group).ToList();
    }

    public List<Channel> AllOf(StreamKind kind)
    {
        lock (_lock) return _all.Where(c => c.Kind == kind).ToList();
    }

    /// <summary>Neueste Filme (nach Added absteigend), optional nur deutschsprachige zuerst.</summary>
    public List<Channel> NewestMovies(int count)
    {
        lock (_lock)
        {
            bool De(Channel c) => System.Text.RegularExpressions.Regex.IsMatch(
                c.Group + " " + c.Name, @"(^|\W)(de|ger|deutsch|german)(\W|$)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return _all.Where(c => c.Kind == StreamKind.Movie)
                       .OrderByDescending(c => De(c))
                       .ThenByDescending(c => c.Added)
                       .Take(count)
                       .ToList();
        }
    }

    public List<Channel> Search(string q)
    {
        q = q.Trim().ToLowerInvariant();
        if (q.Length == 0) return new();
        lock (_lock)
            return _all.Where(c => c.Name.ToLowerInvariant().Contains(q)).Take(300).ToList();
    }

    public IEnumerable<Channel> Favorites()
    {
        lock (_lock)
            return _all.Where(c => State.Favorites.Contains(c.Id)).ToList();
    }

    public Channel? ById(string id)
    {
        lock (_lock) return _all.FirstOrDefault(c => c.Id == id);
    }

    public int TotalCount { get { lock (_lock) return _all.Count; } }
}
