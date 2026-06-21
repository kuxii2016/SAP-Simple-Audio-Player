using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SAP.Models;

public class Playlist : INotifyPropertyChanged
{
    private string _name = "";
    private int _currentIndex = -1;

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public ObservableCollection<Song> Songs { get; set; } = new();
    public int CurrentIndex { get => _currentIndex; set => SetProperty(ref _currentIndex, value); }

    public Song? CurrentSong => CurrentIndex >= 0 && CurrentIndex < Songs.Count ? Songs[CurrentIndex] : null;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Add(Song song) => Songs.Add(song);
    public void Remove(Song song) => Songs.Remove(song);
    public void Clear() { Songs.Clear(); CurrentIndex = -1; }

    public Song? Next()
    {
        if (Songs.Count == 0) return null;
        CurrentIndex = (CurrentIndex + 1) % Songs.Count;
        return CurrentSong;
    }

    public Song? Previous()
    {
        if (Songs.Count == 0) return null;
        CurrentIndex = CurrentIndex <= 0 ? Songs.Count - 1 : CurrentIndex - 1;
        return CurrentSong;
    }

    public void Save(string filePath)
    {
        var data = new PlaylistData { Name = Name, Files = Songs.Select(s => s.FilePath).ToList() };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }

    public static Playlist Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<PlaylistData>(json);
        var playlist = new Playlist { Name = data?.Name ?? "Unnamed" };
        if (data?.Files != null)
        {
            foreach (var f in data.Files)
            {
                if (File.Exists(f))
                    playlist.Songs.Add(new Song { FilePath = f });
            }
        }
        return playlist;
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private class PlaylistData
    {
        public string Name { get; set; } = "";
        public List<string> Files { get; set; } = new();
    }
}
