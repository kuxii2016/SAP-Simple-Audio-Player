using System.IO;
using System.Text.Json;

namespace SAP.Models;

public class EqualizerPreset
{
    public string Name { get; set; } = "Default";
    public float[] Gains { get; set; } = Array.Empty<float>();
    public bool EqualizerEnabled { get; set; }
    public bool IsEqualizerVisible { get; set; }

    private static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SAP", "EqualizerPresets");

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, $"{Name}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(this));
    }

    public static EqualizerPreset? Load(string name)
    {
        var path = Path.Combine(Folder, $"{name}.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<EqualizerPreset>(File.ReadAllText(path)) : null;
    }

    public static string[] ListPresets()
    {
        if (!Directory.Exists(Folder)) return Array.Empty<string>();
        return Directory.GetFiles(Folder, "*.json")
                       .Select(Path.GetFileNameWithoutExtension)
                       .Where(x => x != null)
                       .Cast<string>()
                       .ToArray();
    }

    public static void SaveLast(List<EqualizerBand> bands, bool eqEnabled, bool eqVisible)
    {
        var preset = new EqualizerPreset
        {
            Name = "_last",
            Gains = bands.Select(b => b.Gain).ToArray(),
            EqualizerEnabled = eqEnabled,
            IsEqualizerVisible = eqVisible
        };
        preset.Save();
    }

    public static EqualizerPreset? LoadLast()
    {
        return Load("_last");
    }
}
