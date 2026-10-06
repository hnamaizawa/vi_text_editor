using System.Text.Json;

namespace ViTextEditor;

internal sealed record EditorSettings(int RecentFileLimit = 20, bool ShowFullPathInTitle = true)
{
    public EditorSettings Normalize() => this with
    {
        RecentFileLimit = RecentFileLimit is >= 1 and <= 100 ? RecentFileLimit : 20
    };
}

internal sealed class EditorSettingsStore
{
    private readonly string _path;

    public EditorSettingsStore()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vi_text_editor");
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "settings.json");
    }

    public EditorSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new EditorSettings();
            return (JsonSerializer.Deserialize<EditorSettings>(File.ReadAllText(_path)) ?? new EditorSettings()).Normalize();
        }
        catch
        {
            return new EditorSettings();
        }
    }

    public void Save(EditorSettings settings)
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(settings.Normalize(), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Settings persistence is best effort only.
        }
    }
}
