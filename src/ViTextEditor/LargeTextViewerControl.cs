using System.Text;
using ViTextEditor.Core.Editor;

namespace ViTextEditor;

internal sealed class LargeTextViewerControl : UserControl
{
    private const int IndexStrideLines = 256;
    private const int ScanBufferSize = 1024 * 1024;
    private const int SearchChunkSize = 256 * 1024;
    private const int MaxDisplayLineBytes = 1024 * 1024;
    private const int MaxCachedLines = 1024;

    private readonly DataGridView _grid = new();
    private readonly Label _header = new();
    private readonly Label _status = new();
    private readonly TextBox _command = new();
    private readonly List<Checkpoint> _checkpoints = [];
    private readonly Dictionary<int, string> _lineCache = new();
    private readonly ViOptions _options = ViOptions.Shared;
    private FileStream? _stream;
    private Encoding _encoding = new UTF8Encoding(false);
    private int _preambleLength;
    private long _length;
    private long _lineCount;
    private string? _path;
    private bool _pendingG;
    private string? _lastSearch;
    private bool _lastSearchForward = true;
    private long? _lastMatchOffset;
    private char _commandPrefix;

    static LargeTextViewerControl() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public LargeTextViewerControl()
    {
        Dock = DockStyle.Fill;

        _header.Dock = DockStyle.Top;
        _header.Height = 30;
        _header.Padding = new Padding(8, 7, 8, 0);

        _status.Dock = DockStyle.Bottom;
        _status.Height = 24;
        _status.Padding = new Padding(8, 4, 8, 0);

        _command.Dock = DockStyle.Bottom;
        _command.Visible = false;
        _command.Font = new Font(SelectJapaneseMonospacedFont(), 10.5f);
        _command.KeyDown += CommandOnKeyDown;

        _grid.Dock = DockStyle.Fill;
        _grid.VirtualMode = true;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoGenerateColumns = false;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.None;
        _grid.Font = new Font(SelectJapaneseMonospacedFont(), 10f);
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Line",
            HeaderText = "Y / LINE",
            Width = 95,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Text",
            HeaderText = "TEXT (streaming / read-only)",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.CellValueNeeded += GridOnCellValueNeeded;
        _grid.CurrentCellChanged += (_, _) => UpdateStatus();
        _grid.KeyDown += GridOnKeyDown;
        _grid.KeyPress += GridOnKeyPress;

        Controls.Add(_grid);
        Controls.Add(_command);
        Controls.Add(_status);
        Controls.Add(_header);
    }

    public string? FilePath => _path;
    public long FileLength => _length;

    public bool LoadFile(string path, out string? error)
    {
        error = null;
        try
        {
            CloseFile();
            _path = Path.GetFullPath(path);
            _stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, ScanBufferSize, FileOptions.RandomAccess);
            _length = _stream.Length;
            DetectEncoding();
            BuildSparseLineIndex();
            _grid.RowCount = (int)Math.Min(int.MaxValue, Math.Max(1L, _lineCount));
            if (_grid.RowCount > 0) _grid.CurrentCell = _grid.Rows[0].Cells[0];
            _header.Text = $"LARGE FILE  {Path.GetFileName(_path)}   {_length:N0} bytes   {_lineCount:N0} lines   {_encoding.WebName}   READ-ONLY";
            UpdateStatus();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            CloseFile();
            return false;
        }
    }

    public void FocusViewer() => _grid.Focus();

    public void CloseFile()
    {
        _stream?.Dispose();
        _stream = null;
        _path = null;
        _length = 0;
        _lineCount = 0;
        _checkpoints.Clear();
        _lineCache.Clear();
        _grid.RowCount = 0;
        _lastSearch = null;
        _lastMatchOffset = null;
        _header.Text = "LARGE FILE";
        _status.Text = string.Empty;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _stream?.Dispose();
        base.Dispose(disposing);
    }

    private void DetectEncoding()
    {
        if (_stream is null) return;
        var count = (int)Math.Min(64 * 1024, _length);
        var sample = new byte[count];
        RandomAccess.Read(_stream.SafeFileHandle, sample, 0);
        _preambleLength = 0;
        if (count >= 3 && sample[0] == 0xEF && sample[1] == 0xBB && sample[2] == 0xBF)
        {
            _encoding = new UTF8Encoding(true);
            _preambleLength = 3;
            return;
        }
        if (count >= 2 && sample[0] == 0xFF && sample[1] == 0xFE)
        {
            _encoding = Encoding.Unicode;
            _preambleLength = 2;
            return;
        }
        if (count >= 2 && sample[0] == 0xFE && sample[1] == 0xFF)
        {
            _encoding = Encoding.BigEndianUnicode;
            _preambleLength = 2;
            return;
        }

        try
        {
            _ = new UTF8Encoding(false, true).GetString(sample);
            _encoding = new UTF8Encoding(false);
        }
        catch (DecoderFallbackException)
        {
            _encoding = Encoding.GetEncoding(932);
        }
    }

    private void BuildSparseLineIndex()
    {
        if (_stream is null) return;
        _checkpoints.Clear();
        _checkpoints.Add(new Checkpoint(0, _preambleLength));
        if (_length <= _preambleLength)
        {
            _lineCount = 1;
            return;
        }

        var buffer = new byte[ScanBufferSize];
        long offset = _preambleLength;
        long line = 0;
        byte previous = 0;
        var hasPrevious = false;

        while (offset < _length)
        {
            var requested = (int)Math.Min(buffer.Length, _length - offset);
            var read = RandomAccess.Read(_stream.SafeFileHandle, buffer.AsSpan(0, requested), offset);
            if (read <= 0) break;

            for (var i = 0; i < read; i++)
            {
                var absolute = offset + i;
                var b = buffer[i];
                if (IsLineFeed(previous, b, hasPrevious, absolute))
                {
                    line++;
                    var nextOffset = absolute + 1;
                    if (IsUtf16Encoding()) nextOffset = absolute + 1;
                    if (line % IndexStrideLines == 0) _checkpoints.Add(new Checkpoint(line, nextOffset));
                }
                previous = b;
                hasPrevious = true;
            }
            offset += read;
        }
        _lineCount = line + 1;
    }

    private bool IsLineFeed(byte previous, byte current, bool hasPrevious, long absoluteOffset)
    {
        if (_encoding.CodePage == Encoding.Unicode.CodePage)
        {
            return hasPrevious && previous == 0x0A && current == 0x00 && ((absoluteOffset - _preambleLength) & 1) == 1;
        }
        if (_encoding.CodePage == Encoding.BigEndianUnicode.CodePage)
        {
            return hasPrevious && previous == 0x00 && current == 0x0A && ((absoluteOffset - _preambleLength) & 1) == 1;
        }
        return current == 0x0A;
    }

    private bool IsUtf16Encoding() => _encoding.CodePage is 1200 or 1201;

    private void GridOnCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (e.ColumnIndex == 0)
        {
            e.Value = (e.RowIndex + 1).ToString("N0");
            return;
        }
        e.Value = GetLineText(e.RowIndex);
    }

    private string GetLineText(int zeroBasedLine)
    {
        if (_lineCache.TryGetValue(zeroBasedLine, out var cached)) return cached;
        if (_stream is null) return string.Empty;

        var start = FindLineStart(zeroBasedLine);
        var bytes = ReadLineBytes(start, out var truncated);
        var text = DecodeLine(bytes);
        if (truncated) text += " … [line truncated for display]";
        if (_lineCache.Count >= MaxCachedLines) _lineCache.Clear();
        _lineCache[zeroBasedLine] = text;
        return text;
    }

    private long FindLineStart(long zeroBasedLine)
    {
        if (_stream is null || zeroBasedLine <= 0) return _preambleLength;
        var checkpointIndex = Math.Min(_checkpoints.Count - 1, (int)(zeroBasedLine / IndexStrideLines));
        while (checkpointIndex > 0 && _checkpoints[checkpointIndex].Line > zeroBasedLine) checkpointIndex--;
        var checkpoint = _checkpoints[checkpointIndex];
        var remaining = zeroBasedLine - checkpoint.Line;
        if (remaining <= 0) return checkpoint.Offset;
        return ScanForwardLines(checkpoint.Offset, remaining);
    }

    private long ScanForwardLines(long startOffset, long lines)
    {
        if (_stream is null || lines <= 0) return startOffset;
        var buffer = new byte[64 * 1024];
        var offset = startOffset;
        long found = 0;
        byte previous = 0;
        var hasPrevious = false;
        while (offset < _length)
        {
            var requested = (int)Math.Min(buffer.Length, _length - offset);
            var read = RandomAccess.Read(_stream.SafeFileHandle, buffer.AsSpan(0, requested), offset);
            if (read <= 0) break;
            for (var i = 0; i < read; i++)
            {
                var absolute = offset + i;
                var b = buffer[i];
                if (IsLineFeed(previous, b, hasPrevious, absolute))
                {
                    found++;
                    if (found >= lines) return absolute + 1;
                }
                previous = b;
                hasPrevious = true;
            }
            offset += read;
        }
        return _length;
    }

    private byte[] ReadLineBytes(long start, out bool truncated)
    {
        truncated = false;
        if (_stream is null || start >= _length) return [];
        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        var offset = start;
        byte previous = 0;
        var hasPrevious = false;

        while (offset < _length && output.Length < MaxDisplayLineBytes)
        {
            var requested = (int)Math.Min(buffer.Length, Math.Min(_length - offset, MaxDisplayLineBytes - output.Length));
            var read = RandomAccess.Read(_stream.SafeFileHandle, buffer.AsSpan(0, requested), offset);
            if (read <= 0) break;
            var stop = read;
            for (var i = 0; i < read; i++)
            {
                var absolute = offset + i;
                var b = buffer[i];
                if (IsLineFeed(previous, b, hasPrevious, absolute))
                {
                    stop = IsUtf16Encoding() ? Math.Max(0, i - 1) : i;
                    output.Write(buffer, 0, stop);
                    return TrimCarriageReturn(output.ToArray());
                }
                previous = b;
                hasPrevious = true;
            }
            output.Write(buffer, 0, read);
            offset += read;
        }
        truncated = offset < _length;
        return TrimCarriageReturn(output.ToArray());
    }

    private byte[] TrimCarriageReturn(byte[] bytes)
    {
        if (_encoding.CodePage == 1200 && bytes.Length >= 2 && bytes[^2] == 0x0D && bytes[^1] == 0x00) return bytes[..^2];
        if (_encoding.CodePage == 1201 && bytes.Length >= 2 && bytes[^2] == 0x00 && bytes[^1] == 0x0D) return bytes[..^2];
        if (bytes.Length > 0 && bytes[^1] == 0x0D) return bytes[..^1];
        return bytes;
    }

    private string DecodeLine(byte[] bytes)
    {
        try { return _encoding.GetString(bytes); }
        catch { return Encoding.UTF8.GetString(bytes); }
    }

    private void GridOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control)
        {
            switch (e.KeyCode)
            {
                case Keys.F: MovePage(1, false); Consume(e); return;
                case Keys.B: MovePage(-1, false); Consume(e); return;
                case Keys.D: MovePage(1, true); Consume(e); return;
                case Keys.U: MovePage(-1, true); Consume(e); return;
            }
        }
        if (e.KeyCode == Keys.G)
        {
            if (e.Shift) { _pendingG = false; MoveToLine(_grid.RowCount - 1); }
            else if (_pendingG) { _pendingG = false; MoveToLine(0); }
            else _pendingG = true;
            Consume(e); return;
        }
        _pendingG = false;
        if (e.KeyCode == Keys.J) { MoveLines(1); Consume(e); return; }
        if (e.KeyCode == Keys.K) { MoveLines(-1); Consume(e); return; }
        if (e.KeyCode == Keys.N) { RepeatSearch(e.Shift); Consume(e); return; }
        if (e.Shift && e.KeyCode == Keys.OemSemicolon) { BeginCommand(':'); Consume(e); return; }
        if (e.KeyCode == Keys.OemQuestion) { BeginCommand(e.Shift ? '?' : '/'); Consume(e); }
    }

    private void GridOnKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (e.KeyChar is '/' or '?' or ':') { BeginCommand(e.KeyChar); e.Handled = true; }
    }

    private void BeginCommand(char prefix)
    {
        _commandPrefix = prefix;
        _command.Text = prefix.ToString();
        _command.Visible = true;
        _command.BringToFront();
        _command.Focus();
        _command.SelectionStart = _command.TextLength;
    }

    private void CommandOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { EndCommand(); Consume(e); return; }
        if (e.KeyCode != Keys.Enter) return;
        var value = _command.Text.Length > 1 ? _command.Text[1..] : string.Empty;
        if (_commandPrefix == ':')
        {
            if (_options.TryExecuteSet(value, out var message)) _status.Text = message ?? string.Empty;
        }
        else if (!string.IsNullOrEmpty(value)) Search(value, _commandPrefix == '/');
        EndCommand();
        Consume(e);
    }

    private void EndCommand()
    {
        _command.Visible = false;
        _command.Text = string.Empty;
        _grid.Focus();
    }

    private bool Search(string query, bool forward)
    {
        if (_stream is null || string.IsNullOrEmpty(query)) return false;
        var forceIgnore = query.Contains("\\c", StringComparison.Ordinal);
        var forceCase = query.Contains("\\C", StringComparison.Ordinal);
        query = query.Replace("\\c", string.Empty, StringComparison.Ordinal).Replace("\\C", string.Empty, StringComparison.Ordinal);
        if (query.Length == 0) return false;
        var ignoreCase = forceCase ? false : forceIgnore || _options.IgnoreCase;
        var pattern = _encoding.GetBytes(query);
        var origin = CurrentOffset;
        var found = forward ? FindForward(pattern, origin + 1, ignoreCase) : FindBackward(pattern, origin - 1, ignoreCase);
        if (found is null) found = forward ? FindForward(pattern, _preambleLength, ignoreCase) : FindBackward(pattern, _length - pattern.Length, ignoreCase);
        if (found is null) return false;
        _lastSearch = query;
        _lastSearchForward = forward;
        _lastMatchOffset = found.Value;
        MoveToOffset(found.Value);
        return true;
    }

    private void RepeatSearch(bool reverse)
    {
        if (_lastSearch is null) return;
        Search(_lastSearch, reverse ? !_lastSearchForward : _lastSearchForward);
    }

    private long? FindForward(byte[] pattern, long start, bool ignoreCase)
    {
        if (_stream is null || pattern.Length == 0) return null;
        start = Math.Max(_preambleLength, start);
        if (start >= _length) return null;
        var buffer = new byte[SearchChunkSize + pattern.Length - 1];
        var cursor = start;
        while (cursor < _length)
        {
            var read = RandomAccess.Read(_stream.SafeFileHandle, buffer, cursor);
            if (read < pattern.Length) return null;
            var max = read - pattern.Length;
            for (var i = 0; i <= max; i++) if (BytesEqual(buffer.AsSpan(i, pattern.Length), pattern, ignoreCase)) return cursor + i;
            cursor += Math.Max(1, read - pattern.Length + 1);
        }
        return null;
    }

    private long? FindBackward(byte[] pattern, long start, bool ignoreCase)
    {
        if (_stream is null || pattern.Length == 0) return null;
        var maxStart = Math.Min(start, _length - pattern.Length);
        if (maxStart < _preambleLength) return null;
        var buffer = new byte[SearchChunkSize + pattern.Length - 1];
        var cursor = maxStart;
        while (cursor >= _preambleLength)
        {
            var chunkStart = Math.Max(_preambleLength, cursor - SearchChunkSize + 1);
            var requested = (int)Math.Min(buffer.Length, _length - chunkStart);
            var read = RandomAccess.Read(_stream.SafeFileHandle, buffer.AsSpan(0, requested), chunkStart);
            var max = Math.Min((int)(cursor - chunkStart), read - pattern.Length);
            for (var i = max; i >= 0; i--) if (BytesEqual(buffer.AsSpan(i, pattern.Length), pattern, ignoreCase)) return chunkStart + i;
            if (chunkStart == _preambleLength) break;
            cursor = chunkStart - 1;
        }
        return null;
    }

    private static bool BytesEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, bool ignoreCase)
    {
        for (var i = 0; i < left.Length; i++)
        {
            var a = left[i]; var b = right[i];
            if (ignoreCase)
            {
                if (a is >= (byte)'A' and <= (byte)'Z') a = (byte)(a + 32);
                if (b is >= (byte)'A' and <= (byte)'Z') b = (byte)(b + 32);
            }
            if (a != b) return false;
        }
        return true;
    }

    private long CurrentOffset => FindLineStart(_grid.CurrentCell?.RowIndex ?? 0);

    private void MoveToOffset(long offset)
    {
        var checkpointIndex = _checkpoints.BinarySearch(new Checkpoint(0, offset), CheckpointOffsetComparer.Instance);
        if (checkpointIndex < 0) checkpointIndex = Math.Max(0, ~checkpointIndex - 1);
        var checkpoint = _checkpoints[Math.Min(checkpointIndex, _checkpoints.Count - 1)];
        var line = checkpoint.Line + CountLinesBetween(checkpoint.Offset, offset);
        MoveToLine((int)Math.Min(int.MaxValue - 1, line));
    }

    private long CountLinesBetween(long start, long end)
    {
        if (_stream is null || end <= start) return 0;
        var buffer = new byte[64 * 1024];
        var offset = start;
        long count = 0;
        byte previous = 0;
        var hasPrevious = false;
        while (offset < end)
        {
            var requested = (int)Math.Min(buffer.Length, end - offset);
            var read = RandomAccess.Read(_stream.SafeFileHandle, buffer.AsSpan(0, requested), offset);
            if (read <= 0) break;
            for (var i = 0; i < read; i++)
            {
                var absolute = offset + i;
                var b = buffer[i];
                if (IsLineFeed(previous, b, hasPrevious, absolute)) count++;
                previous = b; hasPrevious = true;
            }
            offset += read;
        }
        return count;
    }

    private void MoveLines(int delta)
    {
        if (_grid.RowCount == 0) return;
        var current = _grid.CurrentCell?.RowIndex ?? 0;
        MoveToLine(Math.Clamp(current + delta, 0, _grid.RowCount - 1));
    }

    private void MovePage(int direction, bool half)
    {
        var rows = Math.Max(1, _grid.DisplayedRowCount(false));
        MoveLines(direction * (half ? Math.Max(1, rows / 2) : Math.Max(1, rows - 1)));
    }

    private void MoveToLine(int line)
    {
        if (_grid.RowCount == 0) return;
        line = Math.Clamp(line, 0, _grid.RowCount - 1);
        _grid.CurrentCell = _grid.Rows[line].Cells[0];
        var shown = Math.Max(1, _grid.DisplayedRowCount(false));
        if (line < _grid.FirstDisplayedScrollingRowIndex || line >= _grid.FirstDisplayedScrollingRowIndex + shown)
            _grid.FirstDisplayedScrollingRowIndex = Math.Max(0, line - shown / 2);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var y = (_grid.CurrentCell?.RowIndex ?? 0) + 1;
        _status.Text = $"X 1, Y {y:N0}   |   {_encoding.WebName}   |   {(_options.IgnoreCase ? "ignorecase" : "case-sensitive")}   |   offset 0x{CurrentOffset:X}";
    }

    private static void Consume(KeyEventArgs e) { e.Handled = true; e.SuppressKeyPress = true; }

    private static string SelectJapaneseMonospacedFont()
    {
        var installed = FontFamily.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in new[] { "BIZ UDGothic", "BIZ UDゴシック", "MS Gothic", "ＭＳ ゴシック", "Consolas" })
            if (installed.Contains(candidate)) return candidate;
        return FontFamily.GenericMonospace.Name;
    }

    private readonly record struct Checkpoint(long Line, long Offset);
    private sealed class CheckpointOffsetComparer : IComparer<Checkpoint>
    {
        public static CheckpointOffsetComparer Instance { get; } = new();
        public int Compare(Checkpoint x, Checkpoint y) => x.Offset.CompareTo(y.Offset);
    }
}
