using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SAP.Models;

public class Song : INotifyPropertyChanged
{
    private string _filePath = "";
    private string _title = "";
    private string _artist = "";
    private string _album = "";
    private string _year = "";
    private string _genre = "";
    private TimeSpan _duration;
    private uint _bitrate;
    private int _trackNumber;
    private string _albumArt = "";

    public string FilePath { get => _filePath; set => SetProperty(ref _filePath, value); }
    public string Title { get => _title; set => SetProperty(ref _title, value); }
    public string Artist { get => _artist; set => SetProperty(ref _artist, value); }
    public string Album { get => _album; set => SetProperty(ref _album, value); }
    public string Year { get => _year; set => SetProperty(ref _year, value); }
    public string Genre { get => _genre; set => SetProperty(ref _genre, value); }
    public TimeSpan Duration { get => _duration; set => SetProperty(ref _duration, value); }
    public uint Bitrate { get => _bitrate; set => SetProperty(ref _bitrate, value); }
    public int TrackNumber { get => _trackNumber; set => SetProperty(ref _trackNumber, value); }
    public string AlbumArt { get => _albumArt; set => SetProperty(ref _albumArt, value); }

    public string DisplayName => string.IsNullOrEmpty(Title) ? System.IO.Path.GetFileName(FilePath) : Title;
    public string DisplayDetails => $"{Artist} - {Album}";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
