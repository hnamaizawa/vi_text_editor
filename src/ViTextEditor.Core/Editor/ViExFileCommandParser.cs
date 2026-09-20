namespace ViTextEditor.Core.Editor;

public enum ViExFileCommandKind
{
    ReloadForce,
    EditAlternate,
    QuitForce,
    WriteCurrent,
    WriteAs
}

public readonly record struct ViExFileCommand(ViExFileCommandKind Kind, string? Argument = null);

public static class ViExFileCommandParser
{
    public static bool TryParse(string commandText, out ViExFileCommand command)
    {
        var text = commandText.Trim();

        if (text is "e!" or "edit!")
        {
            command = new ViExFileCommand(ViExFileCommandKind.ReloadForce);
            return true;
        }

        if (text is "e#" or "e #" or "edit#" or "edit #")
        {
            command = new ViExFileCommand(ViExFileCommandKind.EditAlternate);
            return true;
        }

        if (text is "q!" or "quit!")
        {
            command = new ViExFileCommand(ViExFileCommandKind.QuitForce);
            return true;
        }

        if (text is "w" or "write")
        {
            command = new ViExFileCommand(ViExFileCommandKind.WriteCurrent);
            return true;
        }

        if (TryGetArgument(text, "w", out var shortWriteArgument) ||
            TryGetArgument(text, "write", out shortWriteArgument))
        {
            command = new ViExFileCommand(ViExFileCommandKind.WriteAs, shortWriteArgument);
            return true;
        }

        command = default;
        return false;
    }

    private static bool TryGetArgument(string text, string commandName, out string argument)
    {
        argument = string.Empty;
        if (text.Length <= commandName.Length ||
            !text.StartsWith(commandName, StringComparison.Ordinal) ||
            !char.IsWhiteSpace(text[commandName.Length]))
        {
            return false;
        }

        argument = text[(commandName.Length + 1)..].Trim();
        return argument.Length > 0;
    }
}
