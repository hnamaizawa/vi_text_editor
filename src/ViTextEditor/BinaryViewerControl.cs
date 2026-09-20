using System.Text;
using ViTextEditor.Core.Editor;

namespace ViTextEditor;

internal sealed class BinaryViewerControl : UserControl
{
    private const int BytesPerRow = 16;
    private const int MaxCachedRows = 1024;
    private const int SearchChunkSize = 64 * 1024;
    private const int MaxSearchPatternBytes = 64 * 1024;

    private readonly FastDataGridView _grid = new();
    private readonly Panel _headerPanel = new();
    private readonly Label _header = new();
    private readonly Label _encodingCaption = new();
    private readonly ComboBox _encodingSelector = new();
    private readonly Dictionary<int, BinaryRow> _rowCache = new();
    private FileStream? _stream;
    private long _length;
    private string? _filePath;
    private BinaryTextEncoding _detectedEncoding = BinaryTextEncoding.Utf8;
    private bool _pendingG;
    private byte[]? _lastSearchPattern;
    private string? _lastSearchQuery;
    private bool _lastSearchForward = true;
    private bool _lastSearchIsHex;
    private long? _lastMatchOffset;

    static BinaryViewerControl()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public BinaryViewerControl()
    {
        Dock = DockStyle.Fill;
        Visible = false;

        _headerPanel.Dock = DockStyle.Top;
        _headerPanel.Height = 34;

        _header.Dock = DockStyle.Fill;
        _header.Padding = new Padding(8, 8, 8, 0);
        _header.Text = "BINARY  16 bytes/row   OFFSET | HEX | TEXT";

        _encodingCaption.Dock = DockStyle.Right;
        _encodingCaption.Width = 68;
        _encodingCaption.TextAlign = ContentAlignment.MiddleRight;
        _encodingCaption.Text = "文字コード";

        _encodingSelector.Dock = DockStyle.Right;
        _encodingSelector.Width = 130;
        _encodingSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _encodingSelector.Items.AddRange(
        [
            new EncodingOption(BinaryTextEncoding.Auto, "自動判定"),
            new EncodingOption(BinaryTextEncoding.Utf8, "UTF-8"),
            new EncodingOption(BinaryTextEncoding.ShiftJis, "Shift_JIS"),
            new EncodingOption(BinaryTextEncoding.Utf16Le, "UTF-16 LE"),
            new EncodingOption(BinaryTextEncoding.Utf16Be, "UTF-16 BE"),
            new EncodingOption(BinaryTextEncoding.Ascii, "ASCII")
        ]);
        _encodingSelector.SelectedIndex = 0;
        _encodingSelector.SelectedIndexChanged += (_, _) =>
        {
            _rowCache.Clear();
            _lastSearchPattern = null;
            _lastSearchQuery = null;
            _lastMatchOffset = null;
            UpdateHeader();
            _grid.Invalidate();
            StatusChanged?.Invoke(this, EventArgs.Empty);
        };

        _headerPanel.Controls.Add(_header);
        _headerPanel.Controls.Add(_encodingCaption);
        _headerPanel.Controls.Add(_encodingSelector);

        _grid.Dock = DockStyle.Fill;
        _grid.VirtualMode = true;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grid.AutoGenerateColumns = false;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.None;
        _grid.Font = new Font(SelectJapaneseMonospacedFont(), 10f);
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.RowTemplate.Height = Math.Max(20, _grid.Font.Height + 5);

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Offset",
            HeaderText = "OFFSET",
            Width = 120,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Hex",
            HeaderText = "HEX (00-FF)",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 72,
            MinimumWidth = 420,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Text",
            HeaderText = "TEXT",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 28,
            MinimumWidth = 200,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        _grid.CellValueNeeded += GridOnCellValueNeeded;
        _grid.CurrentCellChanged += (_, _) => StatusChanged?.Invoke(this, EventArgs.Empty);
        _grid.KeyDown += GridOnKeyDown;
        _grid.KeyPress += GridOnKeyPress;

        Controls.Add(_grid);
        Controls.Add(_headerPanel);
    }

    public event Action<bool>? SearchInputRequested;
    public event Action? ExInputRequested;
    public event EventHandler? StatusChanged;

    public string StatusText
    {
        get
        {
            if (_filePath is null) return "BINARY";
            var offset = CurrentByteOffset;
            var width = _length > uint.MaxValue ? 16 : 8;
            var match = _lastMatchOffset is long found ? $"  Match 0x{found.ToString($"X{width}")}" : string.Empty;
            var ic = ViOptions.Shared.IgnoreCase ? "  IC" : string.Empty;
            return $"0x{offset.ToString($"X{width}")} / {_length:N0} bytes  {EffectiveEncodingLabel}{ic}{match}";
        }
    }

    public bool LoadFile(string path, out string? error)
    {
        error = null;
        try
        {
            CloseFile();
            var fullPath = Path.GetFullPath(path);
            _stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.RandomAccess);
            _length = _stream.Length;
            _filePath = fullPath;
            _detectedEncoding = DetectEncoding();
            _rowCache.Clear();
            _pendingG = false;
            _lastSearchPattern = null;
            _lastSearchQuery = null;
            _lastSearchIsHex = false;
            _lastMatchOffset = null;

            var rowCount = (_length + BytesPerRow - 1) / BytesPerRow;
            _grid.RowCount = (int)Math.Min(int.MaxValue, rowCount);
            UpdateHeader();
            if (_grid.RowCount > 0)
            {
                _grid.FirstDisplayedScrollingRowIndex = 0;
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
            }
            Invalidate(true);
            StatusChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            CloseFile();
            error = ex.Message;
            return false;
        }
    }

    public void FocusViewer()
    {
        _grid.Focus();
    }

    public void OptionsChanged()
    {
        UpdateHeader();
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool Search(string query, bool forward)
    {
        if (_stream is null || _length == 0 || string.IsNullOrEmpty(query)) return false;
        if (!TryBuildSearchPattern(query, out var pattern, out var isHex)) return false;

        _lastSearchQuery = query;
        _lastSearchPattern = pattern;
        _lastSearchForward = forward;
        _lastSearchIsHex = isHex;
        _lastMatchOffset = null;
        return FindAndSelect(pattern, forward, CurrentByteOffset, IgnoreCaseForSearch(isHex));
    }

    public bool RepeatSearch(bool reverseDirection)
    {
        if (_lastSearchPattern is null || _lastSearchPattern.Length == 0) return false;
        var forward = reverseDirection ? !_lastSearchForward : _lastSearchForward;
        var origin = _lastMatchOffset ?? CurrentByteOffset;
        return FindAndSelect(_lastSearchPattern, forward, origin, IgnoreCaseForSearch(_lastSearchIsHex));
    }

    public void RefreshCurrentFile()
    {
        if (_filePath is null) return;
        var path = _filePath;
        LoadFile(path, out _);
    }

    public void CloseFile()
    {
        _grid.RowCount = 0;
        _rowCache.Clear();
        _stream?.Dispose();
        _stream = null;
        _length = 0;
        _filePath = null;
        _detectedEncoding = BinaryTextEncoding.Utf8;
        _pendingG = false;
        _lastSearchPattern = null;
        _lastSearchQuery = null;
        _lastSearchIsHex = false;
        _lastMatchOffset = null;
        UpdateHeader();
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream?.Dispose();
            _grid.Dispose();
            _headerPanel.Dispose();
        }
        base.Dispose(disposing);
    }

    private long CurrentByteOffset
    {
        get
        {
            if (_length == 0 || _grid.RowCount == 0) return 0;
            var row = _grid.CurrentCell?.RowIndex ?? _grid.FirstDisplayedScrollingRowIndex;
            if (row < 0) row = 0;
            return Math.Min(_length - 1, (long)row * BytesPerRow);
        }
    }

    private void GridOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control)
        {
            _pendingG = false;
            switch (e.KeyCode)
            {
                case Keys.F:
                    MovePage(1, halfPage: false);
                    Consume(e);
                    return;
                case Keys.B:
                    MovePage(-1, halfPage: false);
                    Consume(e);
                    return;
                case Keys.D:
                    MovePage(1, halfPage: true);
                    Consume(e);
                    return;
                case Keys.U:
                    MovePage(-1, halfPage: true);
                    Consume(e);
                    return;
            }
            return;
        }

        if (e.Alt) return;

        if (e.Shift && e.KeyCode == Keys.OemSemicolon)
        {
            _pendingG = false;
            ExInputRequested?.Invoke();
            Consume(e);
            return;
        }

        if (e.KeyCode == Keys.OemQuestion)
        {
            _pendingG = false;
            RequestSearch(forward: !e.Shift);
            Consume(e);
            return;
        }

        if (e.KeyCode == Keys.G)
        {
            if (e.Shift)
            {
                _pendingG = false;
                MoveToRow(Math.Max(0, _grid.RowCount - 1), center: false);
            }
            else if (_pendingG)
            {
                _pendingG = false;
                MoveToRow(0, center: false);
            }
            else
            {
                _pendingG = true;
            }
            Consume(e);
            return;
        }

        _pendingG = false;
        switch (e.KeyCode)
        {
            case Keys.J:
                MoveRows(1);
                Consume(e);
                return;
            case Keys.K:
                MoveRows(-1);
                Consume(e);
                return;
            case Keys.N:
                RepeatSearch(reverseDirection: e.Shift);
                Consume(e);
                return;
        }
    }

    private void GridOnKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (e.KeyChar == '/')
        {
            RequestSearch(forward: true);
            e.Handled = true;
        }
        else if (e.KeyChar == '?')
        {
            RequestSearch(forward: false);
            e.Handled = true;
        }
        else if (e.KeyChar == ':')
        {
            ExInputRequested?.Invoke();
            e.Handled = true;
        }
    }

    private static void Consume(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void RequestSearch(bool forward)
    {
        SearchInputRequested?.Invoke(forward);
    }

    private void MoveRows(int delta)
    {
        if (_grid.RowCount == 0) return;
        var current = _grid.CurrentCell?.RowIndex ?? 0;
        MoveToRow(Math.Clamp(current + delta, 0, _grid.RowCount - 1), center: false);
    }

    private void MovePage(int direction, bool halfPage)
    {
        if (_grid.RowCount == 0) return;
        var displayed = Math.Max(1, _grid.DisplayedRowCount(includePartialRow: false));
        var amount = halfPage ? Math.Max(1, displayed / 2) : Math.Max(1, displayed - 1);
        MoveRows(direction * amount);
    }

    private void MoveToRow(int rowIndex, bool center)
    {
        if (_grid.RowCount == 0) return;
        var target = Math.Clamp(rowIndex, 0, _grid.RowCount - 1);
        var column = _grid.CurrentCell?.ColumnIndex ?? 0;
        column = Math.Clamp(column, 0, _grid.ColumnCount - 1);
        _grid.CurrentCell = _grid.Rows[target].Cells[column];

        if (center)
        {
            var displayed = Math.Max(1, _grid.DisplayedRowCount(includePartialRow: false));
            _grid.FirstDisplayedScrollingRowIndex = Math.Max(0, target - displayed / 2);
        }
        else if (target == 0 || target == _grid.RowCount - 1)
        {
            _grid.FirstDisplayedScrollingRowIndex = target == 0 ? 0 : Math.Max(0, target - Math.Max(1, _grid.DisplayedRowCount(false)) + 1);
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void GridOnCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (_stream is null || e.RowIndex < 0) return;

        var row = GetRow(e.RowIndex);
        e.Value = e.ColumnIndex switch
        {
            0 => row.Offset,
            1 => row.Hex,
            2 => row.Text,
            _ => string.Empty
        };
    }

    private BinaryRow GetRow(int rowIndex)
    {
        if (_rowCache.TryGetValue(rowIndex, out var cached)) return cached;
        if (_stream is null) return default;

        var offset = (long)rowIndex * BytesPerRow;
        Span<byte> bytes = stackalloc byte[BytesPerRow + 4];
        var available = RandomAccess.Read(_stream.SafeFileHandle, bytes, offset);
        var count = Math.Min(BytesPerRow, available);
        var rowBytes = bytes[..count];

        var hex = new StringBuilder(BytesPerRow * 3 - 1);
        for (var i = 0; i < BytesPerRow; i++)
        {
            if (i > 0) hex.Append(' ');
            hex.Append(i < count ? rowBytes[i].ToString("X2") : "  ");
        }

        byte? previousByte = null;
        if (offset > 0)
        {
            Span<byte> previous = stackalloc byte[1];
            if (RandomAccess.Read(_stream.SafeFileHandle, previous, offset - 1) == 1) previousByte = previous[0];
        }

        var text = DecodeText(bytes[..available], count, previousByte, EffectiveEncoding);
        var result = new BinaryRow(offset.ToString(_length > uint.MaxValue ? "X16" : "X8"), hex.ToString(), text);
        if (_rowCache.Count >= MaxCachedRows) _rowCache.Clear();
        _rowCache[rowIndex] = result;
        return result;
    }

    private bool TryBuildSearchPattern(string query, out byte[] pattern, out bool isHex)
    {
        pattern = Array.Empty<byte>();
        isHex = query.StartsWith("hex:", StringComparison.OrdinalIgnoreCase);
        if (isHex)
        {
            var hex = query[4..]
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(",", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal);
            if (hex.Length == 0 || hex.Length % 2 != 0 || hex.Length / 2 > MaxSearchPatternBytes) return false;

            pattern = new byte[hex.Length / 2];
            for (var i = 0; i < pattern.Length; i++)
            {
                if (!byte.TryParse(hex.AsSpan(i * 2, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out pattern[i]))
                {
                    pattern = Array.Empty<byte>();
                    return false;
                }
            }
            return true;
        }

        var encoding = GetEncoding(EffectiveEncoding);
        pattern = encoding.GetBytes(query);
        return pattern.Length is > 0 and <= MaxSearchPatternBytes;
    }

    private static bool IgnoreCaseForSearch(bool isHex) => !isHex && ViOptions.Shared.IgnoreCase;

    private bool FindAndSelect(byte[] pattern, bool forward, long origin, bool ignoreCase)
    {
        if (_stream is null || pattern.Length == 0 || pattern.LongLength > _length) return false;

        var lastStart = _length - pattern.Length;
        long? found;
        if (forward)
        {
            var start = Math.Min(lastStart + 1, origin + 1);
            found = start <= lastStart ? FindForward(pattern, start, lastStart, ignoreCase) : null;
            if (found is null)
            {
                var wrapMax = Math.Min(lastStart, origin);
                found = wrapMax >= 0 ? FindForward(pattern, 0, wrapMax, ignoreCase) : null;
            }
        }
        else
        {
            var start = Math.Min(lastStart, origin - 1);
            found = start >= 0 ? FindBackward(pattern, start, 0, ignoreCase) : null;
            if (found is null)
            {
                var wrapMin = Math.Max(0, origin + 1);
                found = lastStart >= wrapMin ? FindBackward(pattern, lastStart, wrapMin, ignoreCase) : null;
            }
        }

        if (found is null) return false;

        _lastMatchOffset = found.Value;
        var row = (int)Math.Min(int.MaxValue - 1L, found.Value / BytesPerRow);
        MoveToRow(Math.Min(row, Math.Max(0, _grid.RowCount - 1)), center: true);
        if (_grid.RowCount > 0 && _grid.ColumnCount > 1)
        {
            _grid.CurrentCell = _grid.Rows[Math.Min(row, _grid.RowCount - 1)].Cells[1];
        }
        StatusChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private long? FindForward(byte[] pattern, long minStart, long maxStart, bool ignoreCase)
    {
        if (_stream is null || minStart > maxStart) return null;
        var buffer = new byte[SearchChunkSize + pattern.Length - 1];
        var cursor = minStart;

        while (cursor <= maxStart)
        {
            var startCount = (int)Math.Min(SearchChunkSize, maxStart - cursor + 1);
            var requested = Math.Min(buffer.Length, startCount + pattern.Length - 1);
            var read = RandomAccess.Read(_stream.SafeFileHandle, buffer.AsSpan(0, requested), cursor);
            if (read < pattern.Length) return null;

            var index = IndexOf(buffer.AsSpan(0, read), pattern, ignoreCase, startCount - 1);
            if (index >= 0 && index < startCount) return cursor + index;
            cursor += startCount;
        }

        return null;
    }

    private long? FindBackward(byte[] pattern, long maxStart, long minStart, bool ignoreCase)
    {
        if (_stream is null || maxStart < minStart) return null;
        var buffer = new byte[SearchChunkSize + pattern.Length - 1];
        var cursor = maxStart;

        while (cursor >= minStart)
        {
            var chunkStart = Math.Max(minStart, cursor - SearchChunkSize + 1);
            var startCount = (int)(cursor - chunkStart + 1);
            var requested = Math.Min(buffer.Length, startCount + pattern.Length - 1);
            var read = RandomAccess.Read(_stream.SafeFileHandle, buffer.AsSpan(0, requested), chunkStart);
            if (read >= pattern.Length)
            {
                var index = LastIndexOf(buffer.AsSpan(0, read), pattern, Math.Min(startCount - 1, read - pattern.Length), ignoreCase);
                if (index >= 0) return chunkStart + index;
            }

            if (chunkStart == minStart) break;
            cursor = chunkStart - 1;
        }

        return null;
    }

    private static int IndexOf(ReadOnlySpan<byte> data, ReadOnlySpan<byte> pattern, bool ignoreCase, int maxStart)
    {
        if (!ignoreCase)
        {
            var index = data.IndexOf(pattern);
            return index <= maxStart ? index : -1;
        }

        var limit = Math.Min(maxStart, data.Length - pattern.Length);
        for (var i = 0; i <= limit; i++)
        {
            if (ByteSequenceEquals(data.Slice(i, pattern.Length), pattern, ignoreCase: true)) return i;
        }
        return -1;
    }

    private static int LastIndexOf(ReadOnlySpan<byte> data, ReadOnlySpan<byte> pattern, int maxStart, bool ignoreCase)
    {
        for (var i = Math.Min(maxStart, data.Length - pattern.Length); i >= 0; i--)
        {
            if (ByteSequenceEquals(data.Slice(i, pattern.Length), pattern, ignoreCase)) return i;
        }
        return -1;
    }

    private static bool ByteSequenceEquals(ReadOnlySpan<byte> data, ReadOnlySpan<byte> pattern, bool ignoreCase)
    {
        if (!ignoreCase) return data.SequenceEqual(pattern);
        if (data.Length != pattern.Length) return false;
        for (var i = 0; i < data.Length; i++)
        {
            if (FoldAscii(data[i]) != FoldAscii(pattern[i])) return false;
        }
        return true;
    }

    private static byte FoldAscii(byte value) => value is >= (byte)'A' and <= (byte)'Z' ? (byte)(value + 0x20) : value;

    private BinaryTextEncoding EffectiveEncoding
    {
        get
        {
            if (_encodingSelector.SelectedItem is not EncodingOption option) return _detectedEncoding;
            return option.Encoding == BinaryTextEncoding.Auto ? _detectedEncoding : option.Encoding;
        }
    }

    private string EffectiveEncodingLabel => EffectiveEncoding switch
    {
        BinaryTextEncoding.Utf8 => "UTF-8",
        BinaryTextEncoding.ShiftJis => "Shift_JIS",
        BinaryTextEncoding.Utf16Le => "UTF-16 LE",
        BinaryTextEncoding.Utf16Be => "UTF-16 BE",
        BinaryTextEncoding.Ascii => "ASCII",
        _ => "TEXT"
    };

    private void UpdateHeader()
    {
        var searchHint = _lastSearchQuery is null ? "" : $"    /{_lastSearchQuery}";
        var ic = ViOptions.Shared.IgnoreCase ? "    [ignorecase]" : string.Empty;
        _header.Text = _filePath is null
            ? $"BINARY  16 bytes/row   OFFSET | HEX | TEXT    {EffectiveEncodingLabel}{ic}"
            : $"BINARY  16 bytes/row   OFFSET | HEX | TEXT    {Path.GetFileName(_filePath)}    {_length:N0} bytes    {EffectiveEncodingLabel}{ic}{searchHint}";
    }

    private BinaryTextEncoding DetectEncoding()
    {
        if (_stream is null || _length == 0) return BinaryTextEncoding.Utf8;

        var sampleLength = (int)Math.Min(64 * 1024, _length);
        var sample = new byte[sampleLength];
        var count = RandomAccess.Read(_stream.SafeFileHandle, sample, 0);
        var span = sample.AsSpan(0, count);

        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF) return BinaryTextEncoding.Utf8;
        if (span.Length >= 2 && span[0] == 0xFF && span[1] == 0xFE) return BinaryTextEncoding.Utf16Le;
        if (span.Length >= 2 && span[0] == 0xFE && span[1] == 0xFF) return BinaryTextEncoding.Utf16Be;

        try
        {
            _ = new UTF8Encoding(false, true).GetString(span);
            return BinaryTextEncoding.Utf8;
        }
        catch (DecoderFallbackException)
        {
            return BinaryTextEncoding.ShiftJis;
        }
    }

    private static Encoding GetEncoding(BinaryTextEncoding encoding) => encoding switch
    {
        BinaryTextEncoding.Utf8 => new UTF8Encoding(false),
        BinaryTextEncoding.ShiftJis => Encoding.GetEncoding(932),
        BinaryTextEncoding.Utf16Le => new UnicodeEncoding(bigEndian: false, byteOrderMark: false),
        BinaryTextEncoding.Utf16Be => new UnicodeEncoding(bigEndian: true, byteOrderMark: false),
        _ => Encoding.ASCII
    };

    private static string DecodeText(ReadOnlySpan<byte> bytes, int rowByteCount, byte? previousByte, BinaryTextEncoding encoding) => encoding switch
    {
        BinaryTextEncoding.Utf8 => DecodeUtf8(bytes, rowByteCount),
        BinaryTextEncoding.ShiftJis => DecodeShiftJis(bytes, rowByteCount, previousByte),
        BinaryTextEncoding.Utf16Le => DecodeUtf16(bytes, rowByteCount, bigEndian: false),
        BinaryTextEncoding.Utf16Be => DecodeUtf16(bytes, rowByteCount, bigEndian: true),
        _ => DecodeAscii(bytes[..rowByteCount])
    };

    private static string DecodeAscii(ReadOnlySpan<byte> bytes)
    {
        var result = new StringBuilder(bytes.Length);
        foreach (var value in bytes) result.Append(value is >= 0x20 and <= 0x7E ? (char)value : '.');
        return result.ToString();
    }

    private static string DecodeUtf8(ReadOnlySpan<byte> bytes, int rowByteCount)
    {
        var result = new StringBuilder(rowByteCount);
        var i = 0;
        while (i < rowByteCount && IsUtf8Continuation(bytes[i])) i++;

        while (i < rowByteCount)
        {
            var first = bytes[i];
            if (first < 0x80)
            {
                result.Append(first is >= 0x20 and <= 0x7E ? (char)first : '.');
                i++;
                continue;
            }

            var sequenceLength = first switch
            {
                >= 0xC2 and <= 0xDF => 2,
                >= 0xE0 and <= 0xEF => 3,
                >= 0xF0 and <= 0xF4 => 4,
                _ => 1
            };

            if (sequenceLength == 1 || i + sequenceLength > bytes.Length)
            {
                result.Append('.');
                i++;
                continue;
            }

            var sequence = bytes.Slice(i, sequenceLength);
            if (!sequence[1..].ToArray().All(IsUtf8Continuation))
            {
                result.Append('.');
                i++;
                continue;
            }

            try
            {
                AppendVisibleText(result, new UTF8Encoding(false, true).GetString(sequence));
                i += sequenceLength;
            }
            catch (DecoderFallbackException)
            {
                result.Append('.');
                i++;
            }
        }

        return result.ToString();
    }

    private static string DecodeShiftJis(ReadOnlySpan<byte> bytes, int rowByteCount, byte? previousByte)
    {
        var result = new StringBuilder(rowByteCount);
        var i = 0;
        if (rowByteCount > 0 && previousByte is byte previous && IsShiftJisLead(previous) && IsShiftJisTrail(bytes[0])) i = 1;

        var encoding = Encoding.GetEncoding(932, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
        while (i < rowByteCount)
        {
            var value = bytes[i];
            if (value <= 0x7F)
            {
                result.Append(value is >= 0x20 and <= 0x7E ? (char)value : '.');
                i++;
                continue;
            }

            if (value is >= 0xA1 and <= 0xDF)
            {
                AppendVisibleText(result, encoding.GetString(bytes.Slice(i, 1)));
                i++;
                continue;
            }

            if (IsShiftJisLead(value) && i + 1 < bytes.Length && IsShiftJisTrail(bytes[i + 1]))
            {
                AppendVisibleText(result, encoding.GetString(bytes.Slice(i, 2)));
                i += 2;
                continue;
            }

            result.Append('.');
            i++;
        }

        return result.ToString();
    }

    private static string DecodeUtf16(ReadOnlySpan<byte> bytes, int rowByteCount, bool bigEndian)
    {
        var result = new StringBuilder(rowByteCount / 2);
        var encoding = bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode;
        var i = 0;
        while (i + 1 < rowByteCount)
        {
            var codeUnit = bigEndian
                ? (ushort)((bytes[i] << 8) | bytes[i + 1])
                : (ushort)(bytes[i] | (bytes[i + 1] << 8));
            var byteCount = char.IsHighSurrogate((char)codeUnit) && i + 3 < bytes.Length ? 4 : 2;
            AppendVisibleText(result, encoding.GetString(bytes.Slice(i, byteCount)));
            i += byteCount;
        }
        return result.ToString();
    }

    private static void AppendVisibleText(StringBuilder target, string decoded)
    {
        foreach (var ch in decoded)
        {
            target.Append(char.IsControl(ch) || ch == '\uFFFD' ? '.' : ch);
        }
    }

    private static bool IsUtf8Continuation(byte value) => value is >= 0x80 and <= 0xBF;
    private static bool IsShiftJisLead(byte value) => value is >= 0x81 and <= 0x9F or >= 0xE0 and <= 0xFC;
    private static bool IsShiftJisTrail(byte value) => value is >= 0x40 and <= 0x7E or >= 0x80 and <= 0xFC;

    private static string SelectJapaneseMonospacedFont()
    {
        var installed = FontFamily.Families.Select(font => font.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in new[] { "BIZ UDGothic", "BIZ UDゴシック", "MS Gothic", "ＭＳ ゴシック", "Consolas" })
        {
            if (installed.Contains(candidate)) return candidate;
        }
        return FontFamily.GenericMonospace.Name;
    }

    private readonly record struct BinaryRow(string Offset, string Hex, string Text);
    private sealed record EncodingOption(BinaryTextEncoding Encoding, string Label)
    {
        public override string ToString() => Label;
    }

    private enum BinaryTextEncoding
    {
        Auto,
        Utf8,
        ShiftJis,
        Utf16Le,
        Utf16Be,
        Ascii
    }

    private sealed class FastDataGridView : DataGridView
    {
        public FastDataGridView()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }
}
