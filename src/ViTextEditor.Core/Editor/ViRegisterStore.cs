namespace ViTextEditor.Core.Editor;

public sealed record ViRegisterContent(IReadOnlyList<string> Parts, bool IsLinewise)
{
    public string ToText(string newLine) => IsLinewise
        ? string.Join(newLine, Parts)
        : string.Concat(Parts);
}

public sealed class ViRegisterStore
{
    private readonly Dictionary<char, ViRegisterContent> _named = new();
    private ViRegisterContent? _unnamed;

    public void Yank(char? register, IEnumerable<string> lines)
    {
        Store(register, new ViRegisterContent(lines.ToArray(), IsLinewise: true));
    }

    public void StoreText(char? register, string text, bool linewise)
    {
        Store(register, new ViRegisterContent([text], linewise));
    }

    public bool TryGetContent(char? register, out ViRegisterContent content)
    {
        if (register is null)
        {
            if (_unnamed is null)
            {
                content = new ViRegisterContent(Array.Empty<string>(), IsLinewise: false);
                return false;
            }

            content = _unnamed;
            return true;
        }

        var key = char.ToLowerInvariant(register.Value);
        if (_named.TryGetValue(key, out var value))
        {
            content = value;
            return true;
        }

        content = new ViRegisterContent(Array.Empty<string>(), IsLinewise: false);
        return false;
    }

    public bool TryGet(char? register, out IReadOnlyList<string> lines)
    {
        if (TryGetContent(register, out var content))
        {
            lines = content.Parts;
            return true;
        }

        lines = Array.Empty<string>();
        return false;
    }

    private void Store(char? register, ViRegisterContent content)
    {
        _unnamed = content;

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
            if (existing.IsLinewise && content.IsLinewise)
            {
                _named[key] = new ViRegisterContent(existing.Parts.Concat(content.Parts).ToArray(), IsLinewise: true);
            }
            else if (!existing.IsLinewise && !content.IsLinewise)
            {
                _named[key] = new ViRegisterContent([string.Concat(existing.Parts) + string.Concat(content.Parts)], IsLinewise: false);
            }
            else
            {
                _named[key] = content;
            }
        }
        else
        {
            _named[key] = content;
        }
    }
}
