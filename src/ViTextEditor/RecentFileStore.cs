using System.Text.Json;
using ViTextEditor.Core.IO;

namespace ViTextEditor;

internal sealed class RecentFileStore
{
    private readonly string _settingsPath;
    private readonly RecentFileList _recent = new(15);

    public RecentFileStore()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vi_text_editor");
        Directory.CreateDirectory(root);
        _settingsPath = Path.Combine(root, "recent-files.json");
        Load();
    }

    public IReadOnlyList<string> Files => _recent.Items;

    public void Add(string path)
    {
        _recent.Add(path);
        _recent.RemoveMissing();
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return;
            var items = JsonSerializer.Deserialize<string[]>(File.ReadAllText(_settingsPath)) ?? [];
            _recent.Load(items.Reverse());
            _recent.RemoveMissing();
        }
        catch
        {
            // Corrupt history must never prevent the editor from starting.
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_recent.Items, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Recent-file persistence is best effort only.
        }
    }
}
