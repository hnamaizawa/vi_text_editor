namespace ViTextEditor.Core.Editor;

public sealed class ViRegisterStore
{
    private readonly Dictionary<char, string[]> _named = new();
    private string[]? _unnamed;

    public void Yank(char? register, IEnumerable<string> lines)
    {
        var captured = lines.ToArray();
        _unnamed = captured;

        if (register is null)
        {
            return;
        }

        var requested = register.Value;
        if (!char.IsAsciiLetter(requested))
        {
            return;
        }

        var key = char.ToLowerInvariant(requested);
        if (char.IsUpper(requested) && _named.TryGetValue(key, out var existing))
        {
            _named[key] = existing.Concat(captured).ToArray();
        }
        else
        {
            _named[key] = captured;
        }
    }

    public bool TryGet(char? register, out IReadOnlyList<string> lines)
    {
        if (register is null)
        {
            if (_unnamed is null)
            {
                lines = Array.Empty<string>();
                return false;
            }

            lines = _unnamed;
            return true;
        }

        var key = char.ToLowerInvariant(register.Value);
        if (_named.TryGetValue(key, out var value))
        {
            lines = value;
            return true;
        }

        lines = Array.Empty<string>();
        return false;
    }
}
