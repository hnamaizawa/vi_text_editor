using System.Text;

namespace ViTextEditor.Core.IO;

public sealed record CsvTable(char Delimiter, IReadOnlyList<IReadOnlyList<string>> Rows, bool Truncated);

public static class CsvTableParser
{
    public const int DefaultMaxRows = 100_000;
    public const int DefaultMaxColumns = 512;

    public static CsvTable Parse(string? text, int maxRows = DefaultMaxRows, int maxColumns = DefaultMaxColumns)
    {
        text ??= string.Empty;
        if (maxRows < 1) throw new ArgumentOutOfRangeException(nameof(maxRows));
        if (maxColumns < 1) throw new ArgumentOutOfRangeException(nameof(maxColumns));

        var delimiter = DetectDelimiter(text);
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var truncated = false;

        void AddField()
        {
            if (row.Count < maxColumns) row.Add(field.ToString());
            else truncated = true;
            field.Clear();
        }

        bool AddRow()
        {
            AddField();
            if (rows.Count >= maxRows)
            {
                truncated = true;
                return false;
            }
            rows.Add(row.ToArray());
            row = [];
            return true;
        }

        for (var index = 0; index < text.Length; index++)
        {
            var ch = text[index];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }
                continue;
            }

            if (ch == '"' && field.Length == 0)
            {
                quoted = true;
            }
            else if (ch == delimiter)
            {
                AddField();
            }
            else if (ch is '\r' or '\n')
            {
                if (ch == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                if (!AddRow()) break;
            }
            else
            {
                field.Append(ch);
            }
        }

        var endedWithLineBreak = text.Length > 0 && text[^1] is '\r' or '\n';
        if (!truncated && (!endedWithLineBreak || field.Length > 0 || row.Count > 0)) AddRow();

        if (rows.Count > 0 && rows[0].Count > 0 && rows[0][0].StartsWith('\uFEFF'))
        {
            var first = rows[0].ToArray();
            first[0] = first[0].TrimStart('\uFEFF');
            rows[0] = first;
        }

        return new CsvTable(delimiter, rows, truncated);
    }

    public static char DetectDelimiter(string? text)
    {
        if (string.IsNullOrEmpty(text)) return ',';
        var comma = 0;
        var tab = 0;
        var semicolon = 0;
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var ch = text[index];
            if (ch == '"')
            {
                if (quoted && index + 1 < text.Length && text[index + 1] == '"') index++;
                else quoted = !quoted;
                continue;
            }
            if (quoted) continue;
            if (ch is '\r' or '\n') break;
            if (ch == ',') comma++;
            else if (ch == '\t') tab++;
            else if (ch == ';') semicolon++;
        }

        if (tab > comma && tab >= semicolon) return '\t';
        if (semicolon > comma && semicolon > tab) return ';';
        return ',';
    }
}
