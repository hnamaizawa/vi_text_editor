namespace ViTextEditor.Core.Editor;

public enum ViExFileCommandKind
{
    ReloadForce,
    EditAlternate,
    Edit,
    EditForce,
    Quit,
    QuitForce,
    WriteCurrent,
    WriteCurrentForce,
    WriteFile,
    WriteFileForce,
    WriteQuit,
    WriteQuitForce,
    Xit,
    XitForce,
    SaveAs,
    SaveAsForce
}

public readonly record struct ViExFileCommand(ViExFileCommandKind Kind, string? Argument = null);

public static class ViExFileCommandParser
{
    public static bool TryParse(string commandText, out ViExFileCommand command)
    {
        var text = commandText.Trim();

        if (IsAny(text, "e!", "ed!", "edi!", "edit!"))
        {
            command = new ViExFileCommand(ViExFileCommandKind.ReloadForce);
            return true;
        }

        if (IsAny(text, "e#", "e #", "ed#", "ed #", "edi#", "edi #", "edit#", "edit #"))
        {
            command = new ViExFileCommand(ViExFileCommandKind.EditAlternate);
            return true;
        }

        if (IsAny(text, "q", "qu", "qui", "quit"))
        {
            command = new ViExFileCommand(ViExFileCommandKind.Quit);
            return true;
        }

        if (IsAny(text, "q!", "qu!", "qui!", "quit!"))
        {
            command = new ViExFileCommand(ViExFileCommandKind.QuitForce);
            return true;
        }

        if (TryCommandWithOptionalArgument(text, ["wq!"], out var wqForceArg))
        {
            command = new ViExFileCommand(ViExFileCommandKind.WriteQuitForce, NullIfEmpty(wqForceArg));
            return true;
        }
        if (TryCommandWithOptionalArgument(text, ["wq"], out var wqArg))
        {
            command = new ViExFileCommand(ViExFileCommandKind.WriteQuit, NullIfEmpty(wqArg));
            return true;
        }

        if (TryCommandWithOptionalArgument(text, ["x!", "xit!"], out var xForceArg))
        {
            command = new ViExFileCommand(ViExFileCommandKind.XitForce, NullIfEmpty(xForceArg));
            return true;
        }
        if (TryCommandWithOptionalArgument(text, ["x", "xit"], out var xArg))
        {
            command = new ViExFileCommand(ViExFileCommandKind.Xit, NullIfEmpty(xArg));
            return true;
        }

        if (TryCommandWithRequiredArgument(text, ["sav!", "saveas!"], out var saveAsForceArg))
        {
            command = new ViExFileCommand(ViExFileCommandKind.SaveAsForce, saveAsForceArg);
            return true;
        }
        if (TryCommandWithRequiredArgument(text, ["sav", "saveas"], out var saveAsArg))
        {
            command = new ViExFileCommand(ViExFileCommandKind.SaveAs, saveAsArg);
            return true;
        }

        if (TryCommandWithRequiredArgument(text, ["e!", "ed!", "edi!", "edit!"], out var editForceArg))
        {
            command = new ViExFileCommand(ViExFileCommandKind.EditForce, editForceArg);
            return true;
        }
        if (TryCommandWithRequiredArgument(text, ["e", "ed", "edi", "edit"], out var editArg))
        {
            command = new ViExFileCommand(ViExFileCommandKind.Edit, editArg);
            return true;
        }

        if (TryCommandWithOptionalArgument(text, ["w!", "wr!", "wri!", "writ!", "write!"], out var writeForceArg))
        {
            command = string.IsNullOrWhiteSpace(writeForceArg)
                ? new ViExFileCommand(ViExFileCommandKind.WriteCurrentForce)
                : new ViExFileCommand(ViExFileCommandKind.WriteFileForce, writeForceArg);
            return true;
        }

        if (TryCommandWithOptionalArgument(text, ["w", "wr", "wri", "writ", "write"], out var writeArg))
        {
            command = string.IsNullOrWhiteSpace(writeArg)
                ? new ViExFileCommand(ViExFileCommandKind.WriteCurrent)
                : new ViExFileCommand(ViExFileCommandKind.WriteFile, writeArg);
            return true;
        }

        command = default;
        return false;
    }

    private static bool TryCommandWithRequiredArgument(string text, string[] names, out string argument)
    {
        foreach (var name in names)
        {
            if (TryGetArgument(text, name, out argument)) return true;
        }
        argument = string.Empty;
        return false;
    }

    private static bool TryCommandWithOptionalArgument(string text, string[] names, out string argument)
    {
        foreach (var name in names)
        {
            if (text.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                argument = string.Empty;
                return true;
            }
            if (TryGetArgument(text, name, out argument)) return true;
        }
        argument = string.Empty;
        return false;
    }

    private static bool TryGetArgument(string text, string commandName, out string argument)
    {
        argument = string.Empty;
        if (text.Length <= commandName.Length ||
            !text.StartsWith(commandName, StringComparison.OrdinalIgnoreCase) ||
            !char.IsWhiteSpace(text[commandName.Length]))
        {
            return false;
        }

        argument = text[(commandName.Length + 1)..].Trim();
        return argument.Length > 0;
    }

    private static bool IsAny(string text, params string[] values) =>
        values.Any(value => text.Equals(value, StringComparison.OrdinalIgnoreCase));

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
