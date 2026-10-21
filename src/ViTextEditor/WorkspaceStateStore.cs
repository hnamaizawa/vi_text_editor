using System.Text.Json;

namespace ViTextEditor;

internal sealed record WorkspaceState(
    string[] OpenFiles,
    string? SelectedFile,
    int Left,
    int Top,
    int Width,
    int Height,
    bool Maximized)
{
    public static WorkspaceState Empty { get; } = new([], null, 0, 0, 0, 0, false);
}

internal sealed class WorkspaceStateStore
{
    private readonly string _path;

    public WorkspaceStateStore()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vi_text_editor");
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "workspace-state.json");
    }

    public WorkspaceState Load()
    {
        try
        {
            if (!File.Exists(_path)) return WorkspaceState.Empty;
            var state = JsonSerializer.Deserialize<WorkspaceState>(File.ReadAllText(_path));
            if (state is null) return WorkspaceState.Empty;
            var selectedFile = !string.IsNullOrWhiteSpace(state.SelectedFile) && File.Exists(state.SelectedFile)
                ? Path.GetFullPath(state.SelectedFile)
                : null;
            return state with
            {
                SelectedFile = selectedFile,
                OpenFiles = state.OpenFiles?
                    .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    .Select(Path.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray() ?? []
            };
        }
        catch
        {
            return WorkspaceState.Empty;
        }
    }

    public void Save(WorkspaceState state)
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Workspace restoration is best effort only.
        }
    }
}
