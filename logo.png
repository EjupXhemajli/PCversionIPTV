using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EXIPTV.Models;
using EXIPTV.Player;
using EXIPTV.Services;

namespace EXIPTV;

public partial class MainWindow : Window
{
    private readonly AppState _state;
    private readonly Library _lib;
    private VlcPlayer? _player;
    private StreamKind _currentKind = StreamKind.Live;
    private string _editingId = "";   // "" = neue Playlist

    public MainWindow()
    {
        InitializeComponent();
        _state = Storage.Load();
        _lib = new Library(_state);
        VersionText.Text = "v" + (GetType().Assembly.GetName().Version?.ToString(3) ?? "3.0.0");
        BufferSlider.Value = _state.BufferMs;
        VolumeSlider.Value = _state.Volume;

        Loaded += async (_, __) =>
        {
            InitPlayer();
            RefreshPlaylistItems();
            ShowView("home");
            await LoadAllPlaylistsAsync();
        };
        Closing += (_, __) => { SaveState(); _player?.Dispose(); };
    }

    private void InitPlayer()
    {
        _player = new VlcPlayer();
        _player.SetNetworkCaching(_state.BufferMs);
        _player.Volume = (int)_state.Volume;
        VideoView.MediaPlayer = _player.MediaPlayer;
        _player.StatusChanged += s => Dispatcher.Invoke(() =>
        {
            if (string.IsNullOrEmpty(s)) { PlayerStatus.Visibility = Visibility.Collapsed; }
            else { PlayerStatusText.Text = s; PlayerStatus.Visibility = Visibility.Visible; }
        });
    }

    private void SaveState()
    {
        _state.BufferMs = (int)BufferSlider.Value;
        _state.Volume = VolumeSlider.Value;
        Storage.Save(_state);
    }

    // ================= Laden =================

    private async Task LoadAllPlaylistsAsync()
    {
        // Nacheinander laden (Panels drosseln parallele Anfragen).
        foreach (var p in _state.Playlists.Where(p => p.Enabled))
        {
            await _lib.LoadPlaylistAsync(p);
            RefreshPlaylistItems();
            RefreshCurrentView();
            UpdateHome();
        }
    }

    private async Task ReloadOneAsync(Playlist p)
    {
        await _lib.LoadPlaylistAsync(p);
        RefreshPlaylistItems();
        RefreshCurrentView();
        UpdateHome();
    }

    // ================= Navigation =================

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var tag = (sender as RadioButton)?.Tag?.ToString() ?? "home";
        ShowView(tag);
    }

    private void ShowView(string view)
    {
        HomeView.Visibility = view == "home" ? Visibility.Visible : Visibility.Collapsed;
        SearchView.Visibility = view == "search" ? Visibility.Visible : Visibility.Collapsed;
        CatalogView.Visibility = (view is "live" or "movies" or "series" or "fav") ? Visibility.Visible : Visibility.Collapsed;
        PosterView.Visibility = Visibility.Collapsed;

        switch (view)
        {
            case "home": UpdateHome(); break;
            case "live": _currentKind = StreamKind.Live; LoadCategories("Live-TV"); break;
            case "movies": _currentKind = StreamKind.Movie; LoadCategories("Filme"); break;
            case "series": _currentKind = StreamKind.Series; LoadCategories("Serien"); break;
            case "fav": ShowFavorites(); break;
        }
    }

    private void RefreshCurrentView()
    {
        if (CatalogView.Visibility == Visibility.Visible)
            LoadCategories(_currentKind == StreamKind.Live ? "Live-TV" :
                           _currentKind == StreamKind.Movie ? "Filme" : "Serien");
    }

    private void UpdateHome()
    {
        var total = _lib.TotalCount;
        HomeSubtitle.Text = total > 0
            ? $"{total:N0} Einträge geladen. Wähle Live-TV, Filme oder Serien."
            : "Noch keine Inhalte – füge unter Einstellungen eine Playlist hinzu.";
        HomeNewMovies.ItemsSource = _lib.NewestMovies(24);
    }

    // ================= Kategorien / Listen =================

    private void LoadCategories(string title)
    {
        CatTitle.Text = "Kategorien";
        ContentTitle.Text = title;
        var cats = _lib.Categories(_currentKind);
        var withAll = new List<Library.Category> { new($"Alle ({title})", _lib.AllOf(_currentKind).Count) };
        withAll.AddRange(cats);
        CategoryList.ItemsSource = withAll;
        ContentCount.Text = $"{withAll.Sum(c => c.Count) - withAll[0].Count:N0}";
        ItemList.ItemsSource = null;
        PosterView.Visibility = Visibility.Collapsed;
        CatalogView.Visibility = Visibility.Visible;
    }

    private void Category_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryList.SelectedItem is not Library.Category cat) return;
        bool isAll = cat.Name.StartsWith("Alle (");
        var items = isAll ? _lib.AllOf(_currentKind) : _lib.InCategory(_currentKind, cat.Name);

        if (_currentKind == StreamKind.Live)
        {
            // Live: einfache Liste
            PosterView.Visibility = Visibility.Collapsed;
            ItemList.ItemsSource = items;
            ContentTitle.Text = isAll ? "Alle Sender" : cat.Name;
            ContentCount.Text = $"{items.Count:N0}";
        }
        else
        {
            // Filme/Serien: Poster-Grid
            PosterTitle.Text = isAll ? ContentTitle.Text : cat.Name;
            PosterList.ItemsSource = items;
            PosterView.Visibility = Visibility.Visible;
        }
    }

    private void BackToCategories_Click(object sender, RoutedEventArgs e)
        => PosterView.Visibility = Visibility.Collapsed;

    private void ShowFavorites()
    {
        CatalogView.Visibility = Visibility.Visible;
        PosterView.Visibility = Visibility.Collapsed;
        CatTitle.Text = "Favoriten";
        ContentTitle.Text = "Favoriten";
        CategoryList.ItemsSource = new List<Library.Category> { new("★ Alle Favoriten", _state.Favorites.Count) };
        var favs = _lib.Favorites().ToList();
        ItemList.ItemsSource = favs;
        ContentCount.Text = $"{favs.Count:N0}";
    }

    // ================= Aktivierung / Player =================

    private void Item_Activate(object sender, MouseButtonEventArgs e)
    {
        if (ItemList.SelectedItem is Channel c) PlayChannel(c);
    }

    private void Poster_Activate(object sender, MouseButtonEventArgs e)
    {
        var lb = sender as ListBox;
        if (lb?.SelectedItem is Channel c) PlayChannel(c);
    }

    private void PlayChannel(Channel c)
    {
        if (c.Kind == StreamKind.Series)
        {
            MessageBox.Show("Serien-Episodenauswahl folgt in einer kommenden Version.",
                "EX-IPTV", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrEmpty(c.Url)) return;
        _state.LastChannelId = c.Id;
        NowPlaying.Text = c.Name;
        PlayerOverlay.Visibility = Visibility.Visible;
        PlayerStatusText.Text = "Verbinde …";
        PlayerStatus.Visibility = Visibility.Visible;
        var ua = _state.Playlists.FirstOrDefault(p => c.Id.StartsWith(p.Id + "_"))?.UserAgent ?? "";
        _player?.Play(c.Url, ua);
    }

    private void ClosePlayer_Click(object sender, RoutedEventArgs e)
    {
        _player?.Stop();
        PlayerOverlay.Visibility = Visibility.Collapsed;
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _player?.TogglePause();
        PauseBtn.Content = (_player?.MediaPlayer.IsPlaying ?? false) ? "⏸" : "▶";
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_player != null) _player.Volume = (int)e.NewValue;
    }

    // ================= Suche =================

    private void QuickLive_Click(object sender, RoutedEventArgs e) { NavLive.IsChecked = true; }
    private void QuickMovies_Click(object sender, RoutedEventArgs e) { NavMovies.IsChecked = true; }
    private void QuickSeries_Click(object sender, RoutedEventArgs e) { NavSeries.IsChecked = true; }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Enter) DoSearch(); }
    private void DoSearch_Click(object sender, RoutedEventArgs e) => DoSearch();
    private void DoSearch()
        => SearchResults.ItemsSource = _lib.Search(SearchBox.Text);

    // ================= Einstellungen =================

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        RefreshPlaylistItems();
        SettingsOverlay.Visibility = Visibility.Visible;
    }
    private void CloseSettings_Click(object sender, RoutedEventArgs e)
    { SettingsOverlay.Visibility = Visibility.Collapsed; SaveState(); }

    private void Buffer_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BufferLabel != null) BufferLabel.Text = $"{(int)e.NewValue} ms";
        _player?.SetNetworkCaching((int)e.NewValue);
    }

    private void RefreshPlaylistItems()
    {
        PlaylistItems.ItemsSource = null;
        PlaylistItems.ItemsSource = _state.Playlists;
    }

    // ---- Playlist hinzufügen/bearbeiten ----

    private void AddPlaylist_Click(object sender, RoutedEventArgs e)
    {
        _editingId = "";
        EditTitle.Text = "Playlist hinzufügen";
        EName.Text = ""; EServer.Text = ""; EUser.Text = ""; EPass.Password = ""; EUrl.Text = "";
        TypeXtream.IsChecked = true;
        EditError.Visibility = Visibility.Collapsed;
        EditOverlay.Visibility = Visibility.Visible;
    }

    private void EditPlaylist_Click(object sender, RoutedEventArgs e)
    {
        var id = (sender as FrameworkElement)?.Tag?.ToString();
        var p = _state.Playlists.FirstOrDefault(x => x.Id == id);
        if (p == null) return;
        _editingId = p.Id;
        EditTitle.Text = "Playlist bearbeiten";
        EName.Text = p.Name;
        if (p.Type == PlaylistType.Xtream)
        {
            TypeXtream.IsChecked = true;
            EServer.Text = p.Server; EUser.Text = p.Username; EPass.Password = p.Password;
        }
        else { TypeM3u.IsChecked = true; EUrl.Text = p.Url; }
        EditError.Visibility = Visibility.Collapsed;
        EditOverlay.Visibility = Visibility.Visible;
    }

    private void TypeChanged(object sender, RoutedEventArgs e)
    {
        if (XtreamFields == null) return;
        bool xt = TypeXtream.IsChecked == true;
        XtreamFields.Visibility = xt ? Visibility.Visible : Visibility.Collapsed;
        M3uFields.Visibility = xt ? Visibility.Collapsed : Visibility.Visible;
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
        => EditOverlay.Visibility = Visibility.Collapsed;

    private async void SaveEdit_Click(object sender, RoutedEventArgs e)
    {
        bool xt = TypeXtream.IsChecked == true;
        var name = EName.Text.Trim();
        if (name.Length == 0) name = xt ? "Xtream-Liste" : "M3U-Liste";

        Playlist p;
        bool isNew = _editingId == "";
        if (isNew) { p = new Playlist(); _state.Playlists.Add(p); }
        else { p = _state.Playlists.First(x => x.Id == _editingId); }

        p.Name = name;
        if (xt)
        {
            var server = EServer.Text.Trim();
            if (server.Length > 0 && !server.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                server = "http://" + server;
            if (server.Length == 0 || EUser.Text.Trim().Length == 0)
            { ShowEditError("Server und Benutzername sind erforderlich."); if (isNew) _state.Playlists.Remove(p); return; }
            p.Type = PlaylistType.Xtream;
            p.Server = server;
            p.Username = EUser.Text.Trim();
            if (EPass.Password.Length > 0 || isNew) p.Password = EPass.Password;
        }
        else
        {
            var url = EUrl.Text.Trim();
            if (url.Length == 0) { ShowEditError("M3U-Link ist erforderlich."); if (isNew) _state.Playlists.Remove(p); return; }
            p.Type = PlaylistType.M3u;
            p.Url = url;
        }

        SaveState();
        EditOverlay.Visibility = Visibility.Collapsed;
        RefreshPlaylistItems();
        await ReloadOneAsync(p);
    }

    private void ShowEditError(string msg)
    { EditError.Text = msg; EditError.Visibility = Visibility.Visible; }

    private async void ReloadPlaylist_Click(object sender, RoutedEventArgs e)
    {
        var id = (sender as FrameworkElement)?.Tag?.ToString();
        var p = _state.Playlists.FirstOrDefault(x => x.Id == id);
        if (p != null) await ReloadOneAsync(p);
    }

    private void DeletePlaylist_Click(object sender, RoutedEventArgs e)
    {
        var id = (sender as FrameworkElement)?.Tag?.ToString();
        var p = _state.Playlists.FirstOrDefault(x => x.Id == id);
        if (p == null) return;
        if (MessageBox.Show($"Playlist „{p.Name}“ entfernen?", "EX-IPTV",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _lib.RemovePlaylistChannels(p.Id);
        _state.Playlists.Remove(p);
        SaveState();
        RefreshPlaylistItems();
        RefreshCurrentView();
        UpdateHome();
    }
}
