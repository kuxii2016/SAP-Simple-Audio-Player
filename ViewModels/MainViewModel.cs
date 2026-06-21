using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using SAP.Models;
using SAP.Services;

namespace SAP.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly AudioPlayerService _player = new();
    private readonly DiscordRPCService _discord = new();
    private Playlist? _currentPlaylist;
    private Song? _currentSong;
    private bool _isPlaying;
    private bool _isPaused;
    private double _position;
    private double _volume = 0.8;
    private bool _isEqualizerVisible;
    private bool _equalizerEnabled;
    private double _playlistWidth = 300;
    private string _statusText = "Ready";
    private string _searchText = "";
    private bool _repeat;
    private bool _shuffle;
    private readonly Random _random = new();
    private List<int> _shuffleOrder = new();
    private int _shuffleIndex;

    public ObservableCollection<Playlist> Playlists { get; } = new();
    public ObservableCollection<Song> PlaylistSongs { get; } = new();
    public List<EqualizerBand> EqualizerBands { get; } = new();

    public Song? CurrentSong
    {
        get => _currentSong;
        set { _currentSong = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentSongDisplay)); }
    }

    public string CurrentSongDisplay
    {
        get
        {
            if (_currentSong == null) return "No track loaded";
            return $"{_currentSong.Title} - {_currentSong.Artist}";
        }
    }

    public bool IsPlaying { get => _isPlaying; set { _isPlaying = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowPause)); } }
    public bool IsPaused { get => _isPaused; set { _isPaused = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowPause)); } }
    public bool ShowPause => IsPlaying && !IsPaused;

    public double Position
    {
        get => _position;
        set
        {
            _position = value;
            OnPropertyChanged();
            if (_player.State == NAudio.Wave.PlaybackState.Playing || _player.State == NAudio.Wave.PlaybackState.Paused)
                _player.Position = TimeSpan.FromSeconds(value);
        }
    }

    public double PositionMax => _currentSong?.Duration.TotalSeconds ?? 1;

    public double Volume
    {
        get => _volume;
        set { _volume = value; _player.Volume = (float)value; OnPropertyChanged(); }
    }

    public bool IsEqualizerVisible { get => _isEqualizerVisible; set { _isEqualizerVisible = value; OnPropertyChanged(); } }
    public bool EqualizerEnabled
    {
        get => _equalizerEnabled;
        set
        {
            _equalizerEnabled = value;
            OnPropertyChanged();
            _player.EnableEqualizer(value, EqualizerBands);
        }
    }
    public double PlaylistWidth { get => _playlistWidth; set { _playlistWidth = value; OnPropertyChanged(); } }
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
    public string SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(); FilterPlaylist(); } }
    public bool Repeat { get => _repeat; set { _repeat = value; OnPropertyChanged(); } }
    public bool Shuffle
    {
        get => _shuffle;
        set { _shuffle = value; OnPropertyChanged(); if (value) BuildShuffleOrder(); }
    }

    public ICommand OpenFileCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand LoadPlaylistCommand { get; }
    public ICommand SavePlaylistCommand { get; }
    public ICommand ClearPlaylistCommand { get; }
    public ICommand RemoveFromPlaylistCommand { get; }
    public ICommand ToggleEqualizerCommand { get; }
    public ICommand ToggleShuffleCommand { get; }
    public ICommand ToggleRepeatCommand { get; }
    public ICommand DoubleClickSongCommand { get; }
    public ICommand SaveEqualizerPresetCommand { get; }
    public ICommand LoadEqualizerPresetCommand { get; }

    public MainViewModel()
    {
        _player.SongFinished += OnSongFinished;

        InitEqualizerBands();
        LoadLastEqualizerPreset();

        OpenFileCommand = new RelayCommand(_ => OpenFiles());
        OpenFolderCommand = new RelayCommand(_ => OpenFolder());
        PlayPauseCommand = new RelayCommand(_ => PlayPause());
        StopCommand = new RelayCommand(_ => Stop());
        NextCommand = new RelayCommand(_ => Next());
        PreviousCommand = new RelayCommand(_ => Previous());
        LoadPlaylistCommand = new RelayCommand(_ => LoadPlaylist());
        SavePlaylistCommand = new RelayCommand(_ => SavePlaylist());
        ClearPlaylistCommand = new RelayCommand(_ => ClearPlaylist());
        RemoveFromPlaylistCommand = new RelayCommand(p =>
        {
            if (p is Song song && _currentPlaylist != null)
            {
                _currentPlaylist.Remove(song);
                PlaylistSongs.Remove(song);
            }
        });
        ToggleEqualizerCommand = new RelayCommand(_ => IsEqualizerVisible = !IsEqualizerVisible);
        DoubleClickSongCommand = new RelayCommand(p => { if (p is Song s) PlaySong(s); });
        ToggleShuffleCommand = new RelayCommand(_ => Shuffle = !Shuffle);
        ToggleRepeatCommand = new RelayCommand(_ => Repeat = !Repeat);
        SaveEqualizerPresetCommand = new RelayCommand(_ => SaveEqualizerPreset());
        LoadEqualizerPresetCommand = new RelayCommand(_ => LoadEqualizerPreset());

        _currentPlaylist = new Playlist { Name = "Default" };
        Playlists.Add(_currentPlaylist);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (_, _) => UpdatePosition();
        timer.Start();
    }

    private void InitEqualizerBands()
    {
        var bands = new[]
        {
            ("31 Hz", 31f), ("62 Hz", 62f), ("125 Hz", 125f), ("250 Hz", 250f),
            ("500 Hz", 500f), ("1 kHz", 1000f), ("2 kHz", 2000f), ("4 kHz", 4000f),
            ("8 kHz", 8000f), ("16 kHz", 16000f)
        };
        foreach (var (name, freq) in bands)
        {
            var band = new EqualizerBand { Name = name, Frequency = freq, Gain = 0 };
            band.PropertyChanged += (_, _) =>
            {
                _player.UpdateEqualizer(EqualizerBands);
                EqualizerPreset.SaveLast(EqualizerBands, _equalizerEnabled, _isEqualizerVisible);
            };
            EqualizerBands.Add(band);
        }
    }

    private void OpenFiles()
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Audio Files|*.mp3;*.wav;*.flac;*.ogg;*.aac;*.m4a;*.wma|All Files|*.*"
        };
        if (dlg.ShowDialog() == true)
            _ = AddSongsAsync(dlg.FileNames);
    }

    private void OpenFolder()
    {
        var dlg = new OpenFolderDialog();
        if (dlg.ShowDialog() == true)
        {
            var files = Directory.GetFiles(dlg.FolderName, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".mp3") || f.EndsWith(".wav") || f.EndsWith(".flac") ||
                            f.EndsWith(".ogg") || f.EndsWith(".m4a") || f.EndsWith(".aac") || f.EndsWith(".wma"))
                .ToArray();
            _ = AddSongsAsync(files);
        }
    }

    private async Task AddSongsAsync(string[] filePaths)
    {
        StatusText = $"Loading {filePaths.Length} file(s)...";
        var dispatcher = App.Current.Dispatcher;
        int loaded = 0;
        await Task.Run(() =>
        {
            foreach (var path in filePaths)
            {
                var song = TagReaderService.ReadTags(path);
                if (song == null) continue;
                var s = song;
                dispatcher.Invoke(() =>
                {
                    _currentPlaylist?.Add(s);
                    PlaylistSongs.Add(s);
                });
                Interlocked.Increment(ref loaded);
            }
        });
        if (_shuffle) BuildShuffleOrder();
        StatusText = $"{loaded} tracks loaded";
    }

    private void FilterPlaylist()
    {
        if (_currentPlaylist == null) return;
        PlaylistSongs.Clear();
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _currentPlaylist.Songs
            : _currentPlaylist.Songs.Where(s =>
                s.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                s.Artist.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                s.Album.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        foreach (var s in filtered) PlaylistSongs.Add(s);
    }

    private void PlaySong(Song song)
    {
        if (_currentPlaylist == null) return;
        try
        {
            var idx = _currentPlaylist.Songs.IndexOf(song);
            if (idx >= 0)
            {
                _currentPlaylist.CurrentIndex = idx;
                if (_shuffle)
                {
                    var si = _shuffleOrder.IndexOf(idx);
                    _shuffleIndex = si >= 0 ? si : 0;
                }
            }

            _player.Load(song.FilePath, EqualizerBands);
            CurrentSong = song;
            _player.Play();
            IsPlaying = true;
            IsPaused = false;
            OnPropertyChanged(nameof(PositionMax));
            _discord.UpdatePresence(song.Title, song.Artist, _player.Position, song.Duration, false);
        }
        catch (Exception ex)
        {
            StatusText = $"Cannot play: {Path.GetFileName(song.FilePath)} — {ex.Message}";
            Stop();
        }
    }

    private void PlayPause()
    {
        if (_currentSong == null && PlaylistSongs.Count > 0)
        {
            PlaySong(PlaylistSongs[0]);
            return;
        }

        if (IsPlaying && !IsPaused)
        {
            _player.Pause();
            IsPaused = true;
            if (_currentSong != null)
                _discord.UpdatePresence(_currentSong.Title, _currentSong.Artist, _player.Position, _currentSong.Duration, true);
        }
        else
        {
            _player.Play();
            IsPlaying = true;
            IsPaused = false;
            if (_currentSong != null)
                _discord.UpdatePresence(_currentSong.Title, _currentSong.Artist, _player.Position, _currentSong.Duration, false);
        }
    }

    private void Stop()
    {
        _player.Stop();
        IsPlaying = false;
        IsPaused = false;
        _discord.ClearPresence();
    }

    private void Next()
    {
        if (_currentPlaylist == null || _currentPlaylist.Songs.Count == 0) return;
        Song? next;
        if (_shuffle)
        {
            _shuffleIndex++;
            if (_shuffleIndex >= _shuffleOrder.Count)
            {
                if (_repeat) { BuildShuffleOrder(); _shuffleIndex = 0; }
                else { Stop(); return; }
            }
            next = _currentPlaylist.Songs[_shuffleOrder[_shuffleIndex]];
        }
        else
            next = _currentPlaylist.Next();

        if (next != null) PlaySong(next);
    }

    private void Previous()
    {
        if (_currentPlaylist == null || _currentPlaylist.Songs.Count == 0) return;
        if (_shuffle)
        {
            _shuffleIndex--;
            if (_shuffleIndex < 0) _shuffleIndex = _shuffleOrder.Count - 1;
            var prev = _currentPlaylist.Songs[_shuffleOrder[_shuffleIndex]];
            PlaySong(prev);
        }
        else
        {
            var prev = _currentPlaylist.Previous();
            if (prev != null) PlaySong(prev);
        }
    }

    private void OnSongFinished()
    {
        if (_repeat)
        {
            if (_shuffle)
            {
                _shuffleIndex++;
                if (_shuffleIndex >= _shuffleOrder.Count)
                {
                    BuildShuffleOrder();
                    _shuffleIndex = 0;
                }
                var song = _currentPlaylist?.Songs[_shuffleOrder[_shuffleIndex]];
                if (song != null) App.Current.Dispatcher.Invoke(() => PlaySong(song));
            }
            else
                _player.Play();
        }
        else
            App.Current.Dispatcher.Invoke(Next);
    }

    private void LoadPlaylist()
    {
        var dlg = new OpenFileDialog { Filter = "Playlist|*.playlist", DefaultExt = ".playlist" };
        if (dlg.ShowDialog() == true)
        {
            var pl = Playlist.Load(dlg.FileName);
            _currentPlaylist = pl;
            Playlists.Add(pl);
            PlaylistSongs.Clear();
            foreach (var s in pl.Songs) PlaylistSongs.Add(s);
            StatusText = $"Loaded playlist: {pl.Name} — reading tags...";
            _ = ReadPlaylistTagsAsync(pl);
        }
    }

    private async Task ReadPlaylistTagsAsync(Playlist pl)
    {
        var dispatcher = App.Current.Dispatcher;
        var total = pl.Songs.Count;
        int done = 0;
        await Task.Run(() =>
        {
            for (int i = 0; i < total; i++)
            {
                var song = TagReaderService.ReadTags(pl.Songs[i].FilePath);
                if (song == null) continue;
                var idx = i;
                dispatcher.Invoke(() =>
                {
                    pl.Songs[idx] = song;
                    if (idx < PlaylistSongs.Count)
                        PlaylistSongs[idx] = song;
                });
                Interlocked.Increment(ref done);
            }
        });
        StatusText = $"Loaded playlist: {pl.Name} ({done} tracks)";
        if (_shuffle) BuildShuffleOrder();
    }

    private void SavePlaylist()
    {
        if (_currentPlaylist == null) return;
        var dlg = new SaveFileDialog { Filter = "Playlist|*.playlist", DefaultExt = ".playlist" };
        if (dlg.ShowDialog() == true)
        {
            _currentPlaylist.Save(dlg.FileName);
            StatusText = $"Playlist saved: {dlg.FileName}";
        }
    }

    private void ClearPlaylist()
    {
        Stop();
        _currentPlaylist?.Clear();
        PlaylistSongs.Clear();
        CurrentSong = null;
        StatusText = "Playlist cleared";
    }

    private void UpdatePosition()
    {
        if (_player.State == NAudio.Wave.PlaybackState.Playing ||
            _player.State == NAudio.Wave.PlaybackState.Paused)
        {
            var pos = _player.Position.TotalSeconds;
            if (Math.Abs(_position - pos) > 0.01)
            {
                _position = pos;
                OnPropertyChanged(nameof(Position));
            }
        }
    }

    private void BuildShuffleOrder()
    {
        var count = _currentPlaylist?.Songs.Count ?? 0;
        if (count == 0) { _shuffleOrder.Clear(); return; }
        _shuffleOrder = Enumerable.Range(0, count).ToList();
        for (int i = count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (_shuffleOrder[i], _shuffleOrder[j]) = (_shuffleOrder[j], _shuffleOrder[i]);
        }
        _shuffleIndex = 0;
        // prevent the now-playing song from being first in the new order
        if (_currentPlaylist?.CurrentIndex >= 0 && count > 1 && _shuffleOrder[0] == _currentPlaylist.CurrentIndex)
        {
            int swap = _random.Next(1, count);
            (_shuffleOrder[0], _shuffleOrder[swap]) = (_shuffleOrder[swap], _shuffleOrder[0]);
        }
    }

    private void SaveEqualizerPreset()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save Equalizer Preset",
            Filter = "Equalizer Preset|*.eqp",
            DefaultExt = ".eqp",
            FileName = "My Preset"
        };
        if (dlg.ShowDialog() == true)
        {
            var preset = new EqualizerPreset
            {
                Name = Path.GetFileNameWithoutExtension(dlg.FileName),
                Gains = EqualizerBands.Select(b => b.Gain).ToArray()
            };
            File.WriteAllText(dlg.FileName, System.Text.Json.JsonSerializer.Serialize(preset));
            StatusText = $"EQ preset saved: {preset.Name}";
        }
    }

    private void LoadEqualizerPreset()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Load Equalizer Preset",
            Filter = "Equalizer Preset|*.eqp|All Files|*.*",
            DefaultExt = ".eqp"
        };
        if (dlg.ShowDialog() == true)
        {
            var preset = System.Text.Json.JsonSerializer.Deserialize<EqualizerPreset>(File.ReadAllText(dlg.FileName));
            if (preset?.Gains != null && preset.Gains.Length == EqualizerBands.Count)
            {
                for (int i = 0; i < EqualizerBands.Count; i++)
                    EqualizerBands[i].Gain = preset.Gains[i];
                EqualizerPreset.SaveLast(EqualizerBands, _equalizerEnabled, _isEqualizerVisible);
                StatusText = $"EQ preset loaded: {preset.Name}";
            }
        }
    }

    private void LoadLastEqualizerPreset()
    {
        var last = EqualizerPreset.LoadLast();
        if (last == null) return;
        if (last.Gains != null && last.Gains.Length == EqualizerBands.Count)
        {
            for (int i = 0; i < EqualizerBands.Count; i++)
                EqualizerBands[i].Gain = last.Gains[i];
        }
        EqualizerEnabled = last.EqualizerEnabled;
        IsEqualizerVisible = last.IsEqualizerVisible;
    }

    public void Cleanup()
    {
        EqualizerPreset.SaveLast(EqualizerBands, _equalizerEnabled, _isEqualizerVisible);
        _discord.Dispose();
        _player.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
