using System.Text;

namespace ViTextEditor;

internal sealed class BinaryViewerControl : UserControl
{
    private const int BytesPerRow = 16;
    private const int MaxCachedRows = 1024;

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
            UpdateHeader();
            _grid.Invalidate();
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

        Controls.Add(_grid);
        Controls.Add(_headerPanel);
    }

    public string StatusText => _filePath is null
        ? "BINARY"
        : $"BINARY  {_length:N0} bytes  {EffectiveEncodingLabel}";

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

            var rowCount = (_length + BytesPerRow - 1) / BytesPerRow;
            _grid.RowCount = (int)Math.Min(int.MaxValue, rowCount);
            UpdateHeader();
            if (_grid.RowCount > 0)
            {
                _grid.FirstDisplayedScrollingRowIndex = 0;
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
            }
            Invalidate(true);
            return true;
        }
        catch (Exception ex)
        {
            CloseFile();
            error = ex.Message;
            return false;
        }
    }

    public void RefreshCurrentFile()
    {
        if (_filePath is null)
        {
            return;
        }

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
        UpdateHeader();
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

    private void GridOnCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (_stream is null || e.RowIndex < 0)
        {
            return;
        }

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
        if (_rowCache.TryGetValue(rowIndex, out var cached))
        {
            return cached;
        }

        if (_stream is null)
        {
            return default;
        }

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
            if (RandomAccess.Read(_stream.SafeFileHandle, previous, offset - 1) == 1)
            {
                previousByte = previous[0];
            }
        }

        var text = DecodeText(bytes[..available], count, previousByte, EffectiveEncoding);
        var result = new BinaryRow(offset.ToString(_length > uint.MaxValue ? "X16" : "X8"), hex.ToString(), text);
        if (_rowCache.Count >= MaxCachedRows)
        {
            _rowCache.Clear();
        }
        _rowCache[rowIndex] = result;
        return result;
    }

    private BinaryTextEncoding EffectiveEncoding
    {
        get
        {
            if (_encodingSelector.SelectedItem is not EncodingOption option)
            {
                return _detectedEncoding;
            }
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
        _header.Text = _filePath is null
            ? $"BINARY  16 bytes/row   OFFSET | HEX | TEXT    {EffectiveEncodingLabel}"
            : $"BINARY  16 bytes/row   OFFSET | HEX | TEXT    {Path.GetFileName(_filePath)}    {_length:N0} bytes    {EffectiveEncodingLabel}";
    }

    private BinaryTextEncoding DetectEncoding()
    {
        if (_stream is null || _length == 0)
        {
            return BinaryTextEncoding.Utf8;
        }

        var sampleLength = (int)Math.Min(64 * 1024, _length);
        var sample = new byte[sampleLength];
        var count = RandomAccess.Read(_stream.SafeFileHandle, sample, 0);
        var span = sample.AsSpan(0, count);

        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
        {
            return BinaryTextEncoding.Utf8;
        }
        if (span.Length >= 2 && span[0] == 0xFF && span[1] == 0xFE)
        {
            return BinaryTextEncoding.Utf16Le;
        }
        if (span.Length >= 2 && span[0] == 0xFE && span[1] == 0xFF)
        {
            return BinaryTextEncoding.Utf16Be;
        }

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
        foreach (var value in bytes)
        {
            result.Append(value is >= 0x20 and <= 0x7E ? (char)value : '.');
        }
        return result.ToString();
    }

    private static string DecodeUtf8(ReadOnlySpan<byte> bytes, int rowByteCount)
    {
        var result = new StringBuilder(rowByteCount);
        var i = 0;

        // 前行で開始したUTF-8文字の継続バイトは、前行側でlook-aheadして表示する。
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
                var decoded = new UTF8Encoding(false, true).GetString(sequence);
                AppendVisibleText(result, decoded);
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
        if (rowByteCount > 0 && previousByte is byte previous && IsShiftJisLead(previous) && IsShiftJisTrail(bytes[0]))
        {
            // 前行末から始まった2バイト文字は前行側で表示する。
            i = 1;
        }

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
            if (char.IsControl(ch) || ch == '\uFFFD')
            {
                target.Append('.');
            }
            else
            {
                target.Append(ch);
            }
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
