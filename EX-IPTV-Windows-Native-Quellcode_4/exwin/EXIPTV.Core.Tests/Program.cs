using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EXIPTV.Models;
using EXIPTV.Services;

int failed = 0;
void Check(bool cond, string name)
{
    Console.WriteLine((cond ? "  OK   " : "  FAIL ") + name);
    if (!cond) failed++;
}

Console.WriteLine("== Xtream: category_id als ZAHL ==");
{
    var mock = new MockHandler
    {
        Responder = url =>
        {
            if (url.Contains("get_vod_categories")) return (200, """[{"category_id":15,"category_name":"DE Action 4K"},{"category_id":22,"category_name":"DE Komödie"}]""");
            if (url.Contains("get_live_categories")) return (200, """[{"category_id":1,"category_name":"Nachrichten"}]""");
            if (url.Contains("get_series_categories")) return (200, "[]");
            if (url.Contains("get_live_streams")) return (200, """[{"stream_id":101,"name":"Tagesschau","stream_icon":"http://x/a.png","category_id":1,"added":"1700000000"}]""");
            // stream_id mal Zahl (100) mal String ("101"); category_id als Zahl
            if (url.Contains("get_vod_streams")) return (200, """[{"stream_id":100,"name":"Alter Film","category_id":15,"container_extension":"mp4","added":"1700000000","year":"2020"},{"stream_id":"101","name":"Neuer Film","cover":"http://x/f.jpg","category_id":22,"container_extension":"mkv","added":"1710000000","year":"2025"}]""");
            if (url.Contains("get_series")) return (200, "[]");
            return (200, """{"user_info":{"auth":1}}""");
        }
    };
    var client = new XtreamClient(new HttpClient(mock));
    var p = new Playlist { Id = "pl1", Type = PlaylistType.Xtream, Server = "http://panel:80", Username = "u", Password = "p" };
    var r = await client.LoadAsync(p);
    Check(r.Error == null, "kein Fehler: " + (r.Error ?? ""));
    Check(r.Live == 1, $"1 Live-Sender (got {r.Live})");
    Check(r.Movies == 2, $"2 Filme (got {r.Movies})");
    var movies = r.Channels.Where(c => c.Kind == StreamKind.Movie).ToList();
    // KERNTEST: Filme in RICHTIGER Kategorie (nicht Fallback "Filme")
    Check(movies.Any(c => c.Group == "DE Action 4K"), "Film in 'DE Action 4K'");
    Check(movies.Any(c => c.Group == "DE Komödie"), "Film in 'DE Komödie'");
    Check(!movies.Any(c => c.Group == "Filme"), "kein Film im Fallback 'Filme'");
    // Stream-URL korrekt gebaut
    var neu = movies.First(c => c.Name == "Neuer Film");
    Check(neu.Url == "http://panel:80/movie/u/p/101.mkv", "VOD-URL korrekt: " + neu.Url);
    // Neuheits-Sortierung: added als long
    Check(neu.Added == 1710000000, "added geparst: " + neu.Added);
    var live = r.Channels.First(c => c.Kind == StreamKind.Live);
    Check(live.Url == "http://panel:80/live/u/p/101.ts", "Live-URL korrekt: " + live.Url);
}

Console.WriteLine("== Xtream: Drosselung (429) -> Retry ==");
{
    int calls = 0;
    var mock = new MockHandler
    {
        Responder = url =>
        {
            if (url.Contains("get_live_streams"))
            {
                calls++;
                if (calls < 3) return (429, "");
                return (200, """[{"stream_id":1,"name":"K","category_id":1,"added":"1"}]""");
            }
            if (url.Contains("categories")) return (200, "[]");
            if (url.Contains("get_vod_streams") || url.Contains("get_series")) return (200, "[]");
            return (200, """{"user_info":{"auth":1}}""");
        }
    };
    var client = new XtreamClient(new HttpClient(mock));
    var p = new Playlist { Id = "pl1", Server = "http://x", Username = "u", Password = "p" };
    var r = await client.LoadAsync(p);
    Check(r.Live == 1, $"Retry lädt trotz 429 (got {r.Live} Sender, {calls} Versuche)");
}

Console.WriteLine("== Xtream: fehlende Zugangsdaten ==");
{
    var client = new XtreamClient(new HttpClient(new MockHandler()));
    var p = new Playlist { Server = "", Username = "" };
    var r = await client.LoadAsync(p);
    Check(r.Error != null && r.Error.Contains("Zugangsdaten"), "klare Fehlermeldung: " + r.Error);
}

Console.WriteLine("== M3U-Parser ==");
{
    var m3u = "#EXTM3U\n" +
        "#EXTINF:-1 tvg-logo=\"http://x/l.png\" group-title=\"DE Nachrichten\",Tagesschau 24\n" +
        "http://s/live/ard.ts\n" +
        "#EXTINF:-1 group-title=\"Filme DE\",Testfilm 2025\n" +
        "http://s/movie/testfilm.mp4\n" +
        "#EXTINF:-1 group-title=\"DE Sport\",Sport1\n" +
        "http://s/live/sport1.m3u8\n";
    var parser = new M3UParser();
    var list = parser.Parse(m3u, "pl1");
    Check(list.Count == 3, $"3 Einträge (got {list.Count})");
    Check(list.Count(c => c.Kind == StreamKind.Live) == 2, "2 Live");
    Check(list.Count(c => c.Kind == StreamKind.Movie) == 1, "1 Film (mp4 erkannt)");
    Check(list[0].Logo == "http://x/l.png", "Logo geparst");
    Check(list[0].Group == "DE Nachrichten", "Gruppe geparst");
}

Console.WriteLine("== Storage Roundtrip (Credentials bleiben erhalten) ==");
{
    var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "exiptv_test_" + Guid.NewGuid().ToString("N"));
    Environment.SetEnvironmentVariable("APPDATA", tmp);
    var st = new AppState();
    st.Playlists.Add(new Playlist { Name = "Test", Type = PlaylistType.Xtream, Server = "http://x", Username = "user", Password = "geheim" });
    Storage.Save(st);
    var loaded = Storage.Load();
    Check(loaded.Playlists.Count == 1, "1 Playlist geladen");
    Check(loaded.Playlists[0].Password == "geheim", "Passwort persistiert: " + loaded.Playlists[0].Password);
    Check(loaded.Playlists[0].Server == "http://x", "Server persistiert");
}

Console.WriteLine();
Console.WriteLine(failed == 0 ? "ALLE TESTS BESTANDEN" : $"{failed} TEST(S) FEHLGESCHLAGEN");
return failed == 0 ? 0 : 1;

// ---- Mock-HTTP-Handler: liefert je nach action/pfad feste Antworten ----
class MockHandler : HttpMessageHandler
{
    public Func<string, (int status, string body)> Responder = _ => (200, "[]");
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var (status, body) = Responder(req.RequestUri!.ToString());
        return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(body)
        });
    }
}
