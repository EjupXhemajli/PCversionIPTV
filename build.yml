using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EXIPTV.Models;

namespace EXIPTV.Services;

/// <summary>
/// Lädt Live/VOD/Serien über die Xtream-Codes player_api.php.
/// Bewusst über player_api (nicht get.php), weil Panels den M3U-Massendownload
/// drosseln. Kategorien werden parallel geholt, die großen Stream-Listen per
/// System.Text.Json schnell geparst.
/// </summary>
public class XtreamClient
{
    private readonly HttpClient _http;

    public XtreamClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
    }

    public class LoadResult
    {
        public List<Channel> Channels { get; } = new();
        public int Live, Movies, Series;
        public string? Error;
    }

    private string Base(Playlist p) => p.Server.TrimEnd('/');

    private string Api(Playlist p, string action) =>
        $"{Base(p)}/player_api.php?username={Uri.EscapeDataString(p.Username)}" +
        $"&password={Uri.EscapeDataString(p.Password)}" +
        (string.IsNullOrEmpty(action) ? "" : $"&action={action}");

    private async Task<string> GetAsync(string url, string ua, CancellationToken ct)
    {
        // Bis zu 3 Versuche mit Backoff bei Drosselung (429/503).
        Exception? last = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0) await Task.Delay(1500 * attempt, ct);
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("User-Agent",
                    string.IsNullOrWhiteSpace(ua) ? "VLC/3.0.20 LibVLC/3.0.20" : ua);
                req.Headers.TryAddWithoutValidation("Accept", "application/json, */*");
                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if ((int)resp.StatusCode == 429 || (int)resp.StatusCode == 503)
                { last = new Exception($"Panel drosselt ({(int)resp.StatusCode})"); continue; }
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex) { last = ex; }
        }
        throw last ?? new Exception("unbekannter Fehler");
    }

    public async Task<LoadResult> LoadAsync(Playlist p, CancellationToken ct = default)
    {
        var r = new LoadResult();
        if (string.IsNullOrWhiteSpace(p.Server) || string.IsNullOrWhiteSpace(p.Username))
        { r.Error = "Zugangsdaten fehlen (Server/Benutzer)."; return r; }

        var ua = p.UserAgent;
        string b = Base(p);

        // Kategorien parallel holen (klein, schnell).
        var liveCatT = GetAsync(Api(p, "get_live_categories"), ua, ct);
        var vodCatT = GetAsync(Api(p, "get_vod_categories"), ua, ct);
        var serCatT = GetAsync(Api(p, "get_series_categories"), ua, ct);

        Dictionary<string, string> liveCats, vodCats, serCats;
        try
        {
            await Task.WhenAll(liveCatT, vodCatT, serCatT);
            liveCats = ParseCategories(liveCatT.Result);
            vodCats = ParseCategories(vodCatT.Result);
            serCats = ParseCategories(serCatT.Result);
        }
        catch (Exception ex) { r.Error = "Kategorien: " + ex.Message; return r; }

        // Stream-Listen nacheinander (das sind die großen Payloads).
        try
        {
            var liveJson = await GetAsync(Api(p, "get_live_streams"), ua, ct);
            foreach (var s in ParseStreams(liveJson))
            {
                r.Channels.Add(new Channel
                {
                    Id = $"{p.Id}_l_{s.StreamId}",
                    Name = s.Name,
                    Logo = s.Icon,
                    Group = liveCats.GetValueOrDefault(s.CategoryId, "Allgemein"),
                    Url = $"{b}/live/{p.Username}/{p.Password}/{s.StreamId}.ts",
                    Kind = StreamKind.Live,
                    Added = s.Added
                });
                r.Live++;
            }
        }
        catch (Exception ex) { r.Error = "Live: " + ex.Message; }

        try
        {
            var vodJson = await GetAsync(Api(p, "get_vod_streams"), ua, ct);
            foreach (var s in ParseStreams(vodJson))
            {
                var ext = string.IsNullOrEmpty(s.Ext) ? "mp4" : s.Ext;
                r.Channels.Add(new Channel
                {
                    Id = $"{p.Id}_v_{s.StreamId}",
                    Name = s.Name,
                    Logo = string.IsNullOrEmpty(s.Cover) ? s.Icon : s.Cover,
                    Group = vodCats.GetValueOrDefault(s.CategoryId, "Filme"),
                    Url = $"{b}/movie/{p.Username}/{p.Password}/{s.StreamId}.{ext}",
                    Kind = StreamKind.Movie,
                    Added = s.Added,
                    Year = s.Year,
                    ContainerExt = ext
                });
                r.Movies++;
            }
        }
        catch (Exception ex) { r.Error = (r.Error is null ? "" : r.Error + " | ") + "VOD: " + ex.Message; }

        try
        {
            var serJson = await GetAsync(Api(p, "get_series"), ua, ct);
            foreach (var s in ParseStreams(serJson))
            {
                r.Channels.Add(new Channel
                {
                    Id = $"{p.Id}_s_{s.SeriesId}",
                    Name = s.Name,
                    Logo = string.IsNullOrEmpty(s.Cover) ? s.Icon : s.Cover,
                    Group = serCats.GetValueOrDefault(s.CategoryId, "Serien"),
                    Kind = StreamKind.Series,
                    SeriesId = s.SeriesId,
                    Added = s.LastModified
                });
                r.Series++;
            }
        }
        catch (Exception ex) { r.Error = (r.Error is null ? "" : r.Error + " | ") + "Serien: " + ex.Message; }

        if (r.Channels.Count == 0 && r.Error is null)
            r.Error = "Panel lieferte keine Inhalte.";
        return r;
    }

    // ---- Parsing ----

    private static Dictionary<string, string> ParseCategories(string json)
    {
        var map = new Dictionary<string, string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return map;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var id = ReadStr(el, "category_id");
                var name = ReadStr(el, "category_name");
                if (id.Length > 0) map[id] = name;
            }
        }
        catch { /* leere Map */ }
        return map;
    }

    private struct StreamRow
    {
        public string StreamId, SeriesId, Name, Icon, Cover, CategoryId, Ext, Year;
        public long Added, LastModified;
    }

    private static IEnumerable<StreamRow> ParseStreams(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch { yield break; }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array) yield break;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                yield return new StreamRow
                {
                    StreamId = ReadStr(el, "stream_id"),
                    SeriesId = ReadStr(el, "series_id"),
                    Name = ReadStr(el, "name"),
                    Icon = ReadStr(el, "stream_icon"),
                    Cover = ReadStr(el, "cover"),
                    CategoryId = ReadStr(el, "category_id"),
                    Ext = ReadStr(el, "container_extension"),
                    Year = ReadStr(el, "year"),
                    Added = ReadLong(el, "added"),
                    LastModified = ReadLong(el, "last_modified"),
                };
            }
        }
    }

    // category_id/stream_id kommen mal als Zahl, mal als String -> robust lesen.
    private static string ReadStr(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return "";
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? "",
            JsonValueKind.Number => v.TryGetInt64(out var n) ? n.ToString() : v.GetRawText(),
            _ => ""
        };
    }

    private static long ReadLong(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)) return n;
        if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out var m)) return m;
        return 0;
    }
}
