using System.IO;
using SAP.Models;
using TagLib;
using File = TagLib.File;

namespace SAP.Services;

public static class TagReaderService
{
    public static Song? ReadTags(string filePath)
    {
        try
        {
            var song = new Song { FilePath = filePath };
            using var tagFile = File.Create(filePath);

            if (tagFile.Tag != null)
            {
                song.Title = tagFile.Tag.Title ?? "";
                song.Artist = string.Join(", ", tagFile.Tag.Performers ?? Array.Empty<string>());
                song.Album = tagFile.Tag.Album ?? "";
                song.Year = tagFile.Tag.Year > 0 ? tagFile.Tag.Year.ToString() : "";
                song.Genre = string.Join(", ", tagFile.Tag.Genres ?? Array.Empty<string>());
                song.TrackNumber = (int)tagFile.Tag.Track;
                song.Duration = tagFile.Properties?.Duration ?? TimeSpan.Zero;
                song.Bitrate = (uint)(tagFile.Properties?.AudioBitrate ?? 0);

                var pictures = tagFile.Tag.Pictures;
                if (pictures != null && pictures.Length > 0)
                {
                    var pic = pictures[0];
                    var ext = pic.MimeType switch
                    {
                        "image/jpeg" => "jpg",
                        "image/png" => "png",
                        "image/bmp" => "bmp",
                        _ => "jpg"
                    };
                    var base64 = Convert.ToBase64String(pic.Data.Data);
                    song.AlbumArt = $"data:image/{ext};base64,{base64}";
                }
            }

            return song;
        }
        catch
        {
            return null;
        }
    }

    public static List<Song> ReadTagsBulk(IEnumerable<string> filePaths)
    {
        return filePaths.AsParallel().Select(ReadTags).Where(s => s != null).Cast<Song>().ToList();
    }
}
