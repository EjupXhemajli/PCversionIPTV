using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EXIPTV.Models;

namespace EXIPTV.Services;

public class M3UParser
{
    private readonly HttpClient _http;
    public M3UParser(HttpClient? http = null)
        => _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

    private static readonly Regex Attr = new("([a-zA-Z0-9-]+)=\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex ExtInf = new("#EXTINF:.*?,(.*)$", RegexOptions.Compiled);

    public async Task<(List<Channel> channels, string? error)> LoadUrlAsync(
        string url, string ua, string plId, CancellationToken ct = default)
    {
        string body;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent",
                string.IsNullOrWhiteSpace(ua) ? "VLC/3.0.20 LibVLC/3.0.20" : ua);
            req.Headers.TryAddWithoutValidation("Accept", "*/*");
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                return (new(), $"Server antwortet mit Status {(int)resp.StatusCode}");
            body = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) { return (new(), "nicht erreichbar: " + ex.Message); }

        if (!body.Contains("#EXTINF") && !body.Contains("#EXTM3U"))
            return (new(), "keine gültige M3U-Playlist");

        var list = Parse(body, plId);
        if (list.Count == 0) return (new(), "M3U geladen, aber keine Einträge erkannt");
        return (list, null);
    }

    public List<Channel> Parse(string body, string plId)
    {
        var outList = new List<Channel>();
        var lines = body.Replace("\r\n", "\n").Split('\n');
        Channel? cur = null;
        int idx = 0;
        foreach (var raw in lines)
        {
            var ln = raw.Trim();
            if (ln.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                cur = new Channel { Group = "Allgemein" };
                foreach (Match m in Attr.Matches(ln))
                {
                    switch (m.Groups[1].Value.ToLowerInvariant())
                    {
                        case "tvg-logo": cur.Logo = m.Groups[2].Value; break;
                        case "group-title":
                            if (m.Groups[2].Value.Length > 0) cur.Group = m.Groups[2].Value;
                            break;
                    }
                }
                var mm = ExtInf.Match(ln);
                if (mm.Success) cur.Name = mm.Groups[1].Value.Trim();
            }
            else if (ln.Length > 0 && !ln.StartsWith("#") && cur != null)
            {
                cur.Url = ln;
                var low = ln.ToLowerInvariant();
                if (low.EndsWith(".mp4") || low.EndsWith(".mkv") || low.EndsWith(".avi") || low.Contains("/movie/"))
                    cur.Kind = StreamKind.Movie;
                else if (low.Contains("/series/"))
                    cur.Kind = StreamKind.Series;
                else
                    cur.Kind = StreamKind.Live;
                cur.Id = $"{plId}_{++idx}";
                cur.Added = idx;
                outList.Add(cur);
                cur = null;
            }
        }
        return outList;
    }
}
