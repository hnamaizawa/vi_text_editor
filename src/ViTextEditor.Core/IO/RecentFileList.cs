namespace ViTextEditor.Core.IO;

public sealed class RecentFileList
{
    private readonly int _capacity;
    private readonly List<string> _items = [];

    public RecentFileList(int capacity = 15)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public IReadOnlyList<string> Items => _items;

    public void Load(IEnumerable<string> paths)
    {
        _items.Clear();
        foreach (var path in paths) Add(path);
    }

    public void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        string normalized;
        try { normalized = Path.GetFullPath(path); }
        catch { return; }

        _items.RemoveAll(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        _items.Insert(0, normalized);
        if (_items.Count > _capacity) _items.RemoveRange(_capacity, _items.Count - _capacity);
    }

    public void RemoveMissing(Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        _items.RemoveAll(path => !exists(path));
    }
}
