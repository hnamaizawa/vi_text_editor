namespace ViTextEditor.Core.Editor;

public sealed class ViOptions
{
    public static ViOptions Shared { get; } = new();

    public bool IgnoreCase { get; set; }

    public bool TryExecuteSet(string command, out string? message)
    {
        message = null;
        var text = command.Trim();
        if (!(text.Equals("set", StringComparison.OrdinalIgnoreCase) ||
              text.Equals("se", StringComparison.OrdinalIgnoreCase) ||
              text.StartsWith("set ", StringComparison.OrdinalIgnoreCase) ||
              text.StartsWith("se ", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var firstSpace = text.IndexOf(' ');
        if (firstSpace < 0)
        {
            message = IgnoreCase ? "ignorecase" : "noignorecase";
            return true;
        }

        var arguments = text[(firstSpace + 1)..]
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (arguments.Length == 0)
        {
            message = IgnoreCase ? "ignorecase" : "noignorecase";
            return true;
        }

        foreach (var argument in arguments)
        {
            switch (argument.ToLowerInvariant())
            {
                case "ic":
                case "ignorecase":
                    IgnoreCase = true;
                    message = "ignorecase";
                    break;
                case "noic":
                case "noignorecase":
                    IgnoreCase = false;
                    message = "noignorecase";
                    break;
                case "ic!":
                case "ignorecase!":
                case "invic":
                case "invinorecase":
                    IgnoreCase = !IgnoreCase;
                    message = IgnoreCase ? "ignorecase" : "noignorecase";
                    break;
                case "ic?":
                case "ignorecase?":
                    message = IgnoreCase ? "  ignorecase" : "noignorecase";
                    break;
                case "all":
                    message = IgnoreCase ? "ignorecase" : "noignorecase";
                    break;
                default:
                    message = $"E518: Unknown option: {argument}";
                    return true;
            }
        }

        return true;
    }
}
