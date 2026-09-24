using System.Diagnostics;
using System.Text;
using ScintillaNET;
using ViTextEditor.Core.Editor;
using ViTextEditor.Core.IO;

namespace ViTextEditor;

public sealed class MainForm : Form
{
    private const int SciSetLayoutCache = 2272;
    private const int SciPositionFromPointClose = 2023;
    private const int ScCachePage = 2;
    private const int MaxCommandHistory = 200;

    private readonly Scintilla _editor = new();
    private readonly BinaryViewerControl _binaryViewer = new();
    private readonly TextBox _commandLine = new();
    private readonly ToolStripStatusLabel _modeLabel = new();
    private readonly ToolStripStatusLabel _accessLabel = new();
    private readonly ToolStripStatusLabel _encodingLabel = new();
    private readonly ToolStripStatusLabel _eolLabel = new();
    private readonly ToolStripStatusLabel _positionLabel = new();
    private readonly List<ViRepeatEdit> _insertRepeatEdits = [];
    private readonly ViRegisterStore _registers = new();
    private readonly List<string> _commandHistory = [];
    private readonly List<string> _searchHistory = [];
    private readonly ViKeyProcessor _vi;
    private readonly ViNavigationProcessor _navigation;
    private readonly ViSearchProcessor _search;
    private readonly ViCommandProcessor _commands;
    private ToolStripMenuItem? _referenceModeMenuItem;
    private ToolStripMenuItem? _binaryModeMenuItem;
    private ToolStripMenuItem? _undoMenuItem;
    private ToolStripMenuItem? _redoMenuItem;

    private string? _filePath;
    private string? _alternateFilePath;
    private string? _binaryFilePath;
    private Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private string _newLine = "\r\n";
    private bool _dirty;
    private bool _loading;
    private bool _referenceMode = true;
    private bool _binaryMode;
    private bool _changingBinaryMode;
    private bool _forceClose;
    private bool _capturingInsertRepeat;
    private int _insertRepeatAnchor;
    private EditorMode _lastViMode = EditorMode.Normal;
    private char _commandPrefix;
    private int _historyIndex;
    private string _historyPrefix = string.Empty;
    private string? _commandFeedback;
    private Point? _urlClickStart;

    public MainForm()
    {
        Text = "vi_text_editor";
        Width = 1100;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;

        var menu = BuildMenu();
        var status = BuildStatusBar();

        _editor.Dock = DockStyle.Fill;
        ConfigureEditorAppearance();
        _editor.Margins[0].Type = MarginType.Number;
        _editor.Margins[0].Width = 48;
        _editor.KeyDown += EditorOnKeyDown;
        _editor.KeyPress += EditorOnKeyPress;
        _editor.UpdateUI += (_, _) => UpdateStatus();
        _editor.MouseDown += EditorOnMouseDown;
        _editor.MouseUp += EditorOnMouseUp;
        _editor.MouseMove += EditorOnMouseMove;
        _editor.MouseLeave += (_, _) => _editor.Cursor = Cursors.IBeam;
        _editor.Insert += EditorOnInsert;
        _editor.Delete += EditorOnDelete;
        _editor.SavePointLeft += EditorOnSavePointLeft;
        _editor.SavePointReached += EditorOnSavePointReached;

        _binaryViewer.SearchInputRequested += forward => BeginCommandInput(forward ? '/' : '?');
        _binaryViewer.ExInputRequested += () => BeginCommandInput(':');
        _binaryViewer.StatusChanged += (_, _) => UpdateStatus();

        ConfigureCommandLine();

        Controls.Add(_editor);
        Controls.Add(_binaryViewer);
        Controls.Add(_commandLine);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;

        var adapter = new ScintillaEditorAdapter(_editor);
        _vi = new ViKeyProcessor(adapter, _registers);
        _navigation = new ViNavigationProcessor(adapter);
        _search = new ViSearchProcessor(adapter);
        _commands = new ViCommandProcessor(adapter, _registers);
        _vi.ModeChanged += (_, _) =>
        {
            HandleViModeTransition();
            ApplyCaretStyleForMode();
            UpdateStatus();
        };

        FormClosing += OnFormClosing;
        ApplyCaretStyleForMode();
        ApplyReferenceMode();
        ResetUndoBaseline();
        UpdateTitle();
        UpdateStatus();
    }

    private void EditorOnMouseDown(object? sender, MouseEventArgs e)
    {
        _urlClickStart = e.Button == MouseButtons.Left ? e.Location : null;
    }

    private void EditorOnMouseUp(object? sender, MouseEventArgs e)
    {
        UpdateStatus();
        var start = _urlClickStart;
        _urlClickStart = null;
        if (e.Button != MouseButtons.Left || start is null) return;

        var dragSize = SystemInformation.DragSize;
        if (Math.Abs(e.X - start.Value.X) > dragSize.Width / 2 ||
            Math.Abs(e.Y - start.Value.Y) > dragSize.Height / 2)
        {
            return;
        }

        var url = GetUrlAtPoint(e.Location);
        if (url is null) return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(this, $"URLをブラウザで開けませんでした。\n{url}\n\n{ex.Message}", "vi_text_editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EditorOnMouseMove(object? sender, MouseEventArgs e)
    {
        _editor.Cursor = GetUrlAtPoint(e.Location) is null ? Cursors.IBeam : Cursors.Hand;
    }

    private string? GetUrlAtPoint(Point point)
    {
        var position = _editor.DirectMessage(
            SciPositionFromPointClose,
            new IntPtr(point.X),
            new IntPtr(point.Y)).ToInt32();
        if (position < 0) return null;

        var lineNumber = _editor.LineFromPosition(position);
        if (lineNumber < 0 || lineNumber >= _editor.Lines.Count) return null;
        var line = _editor.Lines[lineNumber];
        var lineOffset = position - line.Position;
        if (lineOffset < 0) return null;

        // Scintilla positions are UTF-8 byte offsets. GetTextRange decodes the prefix,
        // giving UrlDetectionService the correct UTF-16 character index even when
        // Japanese text appears before the URL.
        var characterIndex = _editor.GetTextRange(line.Position, lineOffset).Length;
        return UrlDetectionService.FindAt(line.Text, characterIndex)?.Value;
    }

    private void ConfigureEditorAppearance()
    {
        var fontName = SelectMonospacedJapaneseFont();
        _editor.Styles[Style.Default].Font = fontName;
        _editor.Styles[Style.Default].SizeF = 11f;
        _editor.StyleClearAll();
        _editor.CaretWidth = 3;

        _editor.BufferedDraw = false;
        _editor.DirectMessage(SciSetLayoutCache, new IntPtr(ScCachePage));
    }

    private void ConfigureCommandLine()
    {
        _commandLine.Dock = DockStyle.Bottom;
        _commandLine.Visible = false;
        _commandLine.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        _commandLine.Font = new Font(SelectMonospacedJapaneseFont(), 10.5f);
        _commandLine.KeyDown += CommandLineOnKeyDown;
    }

    private static string SelectMonospacedJapaneseFont()
    {
        var installed = FontFamily.Families
            .Select(font => font.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in new[] { "BIZ UDGothic", "BIZ UDゴシック", "MS Gothic", "ＭＳ ゴシック" })
        {
            if (installed.Contains(candidate)) return candidate;
        }
        return "Consolas";
    }

    private void ApplyCaretStyleForMode()
    {
        if (_vi is null) return;
        _editor.CaretStyle = _vi.Mode == EditorMode.Normal ? CaretStyle.Block : CaretStyle.Line;
        _editor.CaretWidth = 3;
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("ファイル(&F)");
        file.DropDownItems.Add(new ToolStripMenuItem("新規(&N)", null, (_, _) => NewDocument(), Keys.Control | Keys.N));
        file.DropDownItems.Add(new ToolStripMenuItem("開く(&O)...", null, (_, _) => OpenDocument(), Keys.Control | Keys.O));
        file.DropDownItems.Add(new ToolStripMenuItem("バイナリとして開く(&B)...", null, (_, _) => OpenBinaryDocument()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(new ToolStripMenuItem("保存(&S)", null, (_, _) => SaveDocument(), Keys.Control | Keys.S));
        file.DropDownItems.Add(new ToolStripMenuItem("名前を付けて保存(&A)...", null, (_, _) => SaveDocumentAs()));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(new ToolStripMenuItem("終了(&X)", null, (_, _) => Close()));

        var edit = new ToolStripMenuItem("編集(&E)");
        _undoMenuItem = new ToolStripMenuItem("元に戻す(&U)", null, (_, _) => _editor.Undo(), Keys.Control | Keys.Z);
        _redoMenuItem = new ToolStripMenuItem("やり直し(&R)", null, (_, _) => _editor.Redo(), Keys.Control | Keys.Y);
        edit.DropDownItems.Add(_undoMenuItem);
        edit.DropDownItems.Add(_redoMenuItem);

        var mode = new ToolStripMenuItem("モード(&M)");
        _referenceModeMenuItem = new ToolStripMenuItem("参照モード(&R)") { CheckOnClick = true, Checked = true };
        _referenceModeMenuItem.CheckedChanged += (_, _) =>
        {
            _referenceMode = _referenceModeMenuItem.Checked;
            ApplyReferenceMode();
        };
        mode.DropDownItems.Add(_referenceModeMenuItem);

        var view = new ToolStripMenuItem("表示(&V)");
        _binaryModeMenuItem = new ToolStripMenuItem("バイナリモード(&B)") { CheckOnClick = true };
        _binaryModeMenuItem.CheckedChanged += (_, _) =>
        {
            if (!_changingBinaryMode) SetBinaryMode(_binaryModeMenuItem.Checked);
        };
        view.DropDownItems.Add(_binaryModeMenuItem);

        var help = new ToolStripMenuItem("ヘルプ(&H)");
        help.DropDownItems.Add(new ToolStripMenuItem("viキーバインド", null, (_, _) => ShowKeyBindings()));
        help.DropDownItems.Add(new ToolStripMenuItem("バージョン情報", null, (_, _) => MessageBox.Show(this, "vi_text_editor v0.1.12", "バージョン情報", MessageBoxButtons.OK, MessageBoxIcon.Information)));

        menu.Items.AddRange([file, edit, mode, view, help]);
        return menu;
    }

    private StatusStrip BuildStatusBar()
    {
        var status = new StatusStrip();
        _modeLabel.AutoSize = false;
        _modeLabel.Width = 120;
        _accessLabel.AutoSize = false;
        _accessLabel.Width = 90;
        _encodingLabel.Spring = true;
        _encodingLabel.TextAlign = ContentAlignment.MiddleRight;
        status.Items.AddRange([_modeLabel, _accessLabel, _encodingLabel, _eolLabel, _positionLabel]);
        return status;
    }

    private void ApplyReferenceMode()
    {
        if (_referenceMode && _vi is not null) _vi.Handle("Esc");
        _editor.ReadOnly = _referenceMode;
        if (_undoMenuItem is not null) _undoMenuItem.Enabled = !_referenceMode && !_binaryMode;
        if (_redoMenuItem is not null) _redoMenuItem.Enabled = !_referenceMode && !_binaryMode;
        UpdateStatus();
    }

    private void SetBinaryMode(bool enabled)
    {
        if (enabled)
        {
            if (_filePath is null)
            {
                MessageBox.Show(this, "現在ファイルがありません。『ファイル > バイナリとして開く』から直接開くこともできます。", "vi_text_editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetBinaryMenuChecked(false);
                return;
            }

            if (!EnterBinaryMode(_filePath))
            {
                SetBinaryMenuChecked(false);
                return;
            }
        }
        else
        {
            LeaveBinaryMode();
        }
    }

    private bool EnterBinaryMode(string path)
    {
        if (!_binaryViewer.LoadFile(path, out var error))
        {
            MessageBox.Show(this, error ?? "バイナリ表示を開始できません。", "vi_text_editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        _binaryFilePath = Path.GetFullPath(path);
        _binaryMode = true;
        _commandLine.Visible = false;
        _editor.Visible = false;
        _binaryViewer.Visible = true;
        _binaryViewer.BringToFront();
        _binaryViewer.FocusViewer();
        if (_undoMenuItem is not null) _undoMenuItem.Enabled = false;
        if (_redoMenuItem is not null) _redoMenuItem.Enabled = false;
        UpdateTitle();
        UpdateStatus();
        return true;
    }

    private void LeaveBinaryMode()
    {
        _binaryMode = false;
        _binaryFilePath = null;
        _binaryViewer.Visible = false;
        _binaryViewer.CloseFile();
        _editor.Visible = true;
        _editor.BringToFront();
        _editor.Focus();
        if (_undoMenuItem is not null) _undoMenuItem.Enabled = !_referenceMode;
        if (_redoMenuItem is not null) _redoMenuItem.Enabled = !_referenceMode;
        UpdateTitle();
        UpdateStatus();
    }

    private void OpenBinaryDocument()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "すべてのファイル|*.*",
            CheckFileExists = true,
            Title = "バイナリとして開く"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        if (EnterBinaryMode(dialog.FileName)) SetBinaryMenuChecked(true);
    }

    private void SetBinaryMenuChecked(bool value)
    {
        if (_binaryModeMenuItem is null) return;
        _changingBinaryMode = true;
        try { _binaryModeMenuItem.Checked = value; }
        finally { _changingBinaryMode = false; }
    }

    private void ResetUndoBaseline()
    {
        _editor.EmptyUndoBuffer();
        SetDirty(false);
    }

    private void HandleViModeTransition()
    {
        if (_vi.IsRepeating)
        {
            _lastViMode = _vi.Mode;
            return;
        }

        if (_lastViMode != EditorMode.Insert && _vi.Mode == EditorMode.Insert && _vi.IsCapturingInsertRepeat)
        {
            _insertRepeatEdits.Clear();
            _insertRepeatAnchor = _editor.CurrentPosition;
            _capturingInsertRepeat = true;
        }
        else if (_lastViMode == EditorMode.Insert && _vi.Mode == EditorMode.Normal && _capturingInsertRepeat)
        {
            _capturingInsertRepeat = false;
            _vi.CommitInsertRepeat(_insertRepeatEdits.ToArray());
            _insertRepeatEdits.Clear();
        }

        _lastViMode = _vi.Mode;
    }

    private void EditorOnInsert(object? sender, ModificationEventArgs e)
    {
        if (_capturingInsertRepeat && !_vi.IsRepeating && !_loading && !string.IsNullOrEmpty(e.Text))
        {
            _insertRepeatEdits.Add(ViRepeatEdit.Insert(e.Position - _insertRepeatAnchor, e.Text));
        }
    }

    private void EditorOnDelete(object? sender, ModificationEventArgs e)
    {
        if (_capturingInsertRepeat && !_vi.IsRepeating && !_loading && !string.IsNullOrEmpty(e.Text))
        {
            _insertRepeatEdits.Add(ViRepeatEdit.Delete(e.Position - _insertRepeatAnchor, e.Text));
        }
    }

    private void EditorOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_binaryMode) return;

        if (_referenceMode && e.Control && (e.KeyCode == Keys.Z || e.KeyCode == Keys.Y || e.KeyCode == Keys.R))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        var token = ToViToken(e);
        if (token is null) return;

        if (_referenceMode && IsMutatingViToken(token))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (_vi.Mode == EditorMode.Normal && token is ":" or "/" or "?")
        {
            BeginCommandInput(token[0]);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (_vi.Mode == EditorMode.Normal && token is "n" or "N")
        {
            _search.Repeat(reverseDirection: token == "N");
            _editor.ScrollCaret();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (_vi.Mode == EditorMode.Normal && !_vi.HasPendingCommand && _navigation.Handle(token))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (_vi.Handle(token))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void EditorOnKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (_binaryMode || _vi.Mode != EditorMode.Normal || _commandLine.Visible) return;

        var token = e.KeyChar switch
        {
            '^' => "^",
            ':' => ":",
            '/' => "/",
            '?' => "?",
            '.' => ".",
            _ => null
        };

        if (token is ":" or "/" or "?")
        {
            BeginCommandInput(token[0]);
            e.Handled = true;
            return;
        }

        if (token is "^" or ".")
        {
            if (!_referenceMode || token != ".") _vi.Handle(token);
            e.Handled = true;
            return;
        }

        if (!char.IsControl(e.KeyChar)) e.Handled = true;
    }

    private static bool IsMutatingViToken(string token) => token is "i" or "a" or "o" or "O" or "x" or "d" or "D" or "c" or "p" or "P" or "." or "u" or "Ctrl+r";

    private static string? ToViToken(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) return "Esc";
        if (e.Control && e.KeyCode == Keys.R) return "Ctrl+r";
        if (e.Control && e.KeyCode == Keys.F) return "Ctrl+f";
        if (e.Control && e.KeyCode == Keys.B) return "Ctrl+b";
        if (e.Control && e.KeyCode == Keys.D) return "Ctrl+d";
        if (e.Control && e.KeyCode == Keys.U) return "Ctrl+u";
        if (e.Control || e.Alt) return null;
        if (e.Shift && e.KeyCode == Keys.OemSemicolon) return ":";
        if (e.Shift && e.KeyCode == Keys.OemQuestion) return "?";
        if (e.Shift)
        {
            return e.KeyCode switch
            {
                Keys.D => "D", Keys.E => "E", Keys.G => "G", Keys.O => "O", Keys.P => "P", Keys.W => "W", Keys.B => "B", Keys.N => "N", Keys.D6 => "^", Keys.D4 => "$", _ => null
            };
        }
        if (e.KeyCode == Keys.OemQuestion) return "/";
        if (e.KeyCode == Keys.OemPeriod) return ".";
        return e.KeyCode switch
        {
            Keys.I => "i", Keys.A => "a", Keys.O => "o", Keys.H => "h", Keys.J => "j", Keys.K => "k", Keys.L => "l",
            Keys.W => "w", Keys.B => "b", Keys.C => "c", Keys.E => "e", Keys.G => "g", Keys.D => "d", Keys.Y => "y", Keys.X => "x",
            Keys.P => "p", Keys.U => "u", Keys.N => "n", Keys.D0 => "0", _ => null
        };
    }

    private void BeginCommandInput(char prefix)
    {
        _commandPrefix = prefix;
        _commandLine.Text = prefix.ToString();
        _commandLine.Visible = true;
        _commandLine.BringToFront();
        _commandLine.Focus();
        _commandLine.SelectionStart = _commandLine.TextLength;
        var history = GetCurrentHistory();
        _historyIndex = history.Count;
        _historyPrefix = string.Empty;
        _commandFeedback = null;
        UpdateStatus();
    }

    private void CommandLineOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            EndCommandInput();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode is Keys.Up or Keys.Down)
        {
            NavigateHistory(e.KeyCode == Keys.Up ? -1 : 1);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Back && _commandLine.SelectionStart <= 1 && _commandLine.SelectionLength == 0)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode != Keys.Enter) return;

        var value = _commandLine.Text.Length > 1 ? _commandLine.Text[1..] : string.Empty;
        AddHistory(value);

        if (_binaryMode && _commandPrefix is '/' or '?')
        {
            _binaryViewer.Search(value, forward: _commandPrefix == '/');
            EndCommandInput();
            UpdateStatus();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (_commandPrefix == ':' && ViExFileCommandParser.TryParse(value, out var fileCommand))
        {
            EndCommandInput();
            ExecuteFileCommand(fileCommand);
            UpdateStatus();
            ShowCommandFeedback();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (_binaryMode && _commandPrefix == ':')
        {
            var actedBinary = _commands.IsOptionCommand(value) && _commands.Execute(value);
            if (!actedBinary && !_commands.IsOptionCommand(value)) _commandFeedback = "BINARY: :set options and file commands only";
            else _commandFeedback = _commands.LastMessage;
            _binaryViewer.OptionsChanged();
            EndCommandInput();
            UpdateStatus();
            ShowCommandFeedback();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        var blockedMutation = _commandPrefix == ':' && _referenceMode && _commands.IsMutatingCommand(value);
        var acted = blockedMutation
            ? false
            : _commandPrefix switch
            {
                ':' => _commands.Execute(value),
                '/' => _search.Search(value, forward: true),
                '?' => _search.Search(value, forward: false),
                _ => false
            };

        if (blockedMutation) _commandFeedback = "参照モードでは変更コマンドを実行できません";
        else if (_commandPrefix == ':') _commandFeedback = _commands.LastMessage;

        EndCommandInput();
        if (acted) _editor.ScrollCaret();
        UpdateStatus();
        ShowCommandFeedback();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private List<string> GetCurrentHistory() => _commandPrefix == ':' ? _commandHistory : _searchHistory;

    private void AddHistory(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var history = GetCurrentHistory();
        history.RemoveAll(item => string.Equals(item, value, StringComparison.Ordinal));
        history.Add(value);
        if (history.Count > MaxCommandHistory) history.RemoveAt(0);
        _historyIndex = history.Count;
        _historyPrefix = string.Empty;
    }

    private void NavigateHistory(int direction)
    {
        var history = GetCurrentHistory();
        if (history.Count == 0) return;
        if (_historyPrefix.Length == 0 && _historyIndex == history.Count)
        {
            _historyPrefix = _commandLine.Text.Length > 1 ? _commandLine.Text[1..] : string.Empty;
        }

        var index = _historyIndex;
        while (true)
        {
            index += direction;
            if (index < 0 || index >= history.Count)
            {
                if (direction > 0 && index >= history.Count)
                {
                    _historyIndex = history.Count;
                    _commandLine.Text = _commandPrefix + _historyPrefix;
                    _commandLine.SelectionStart = _commandLine.TextLength;
                }
                return;
            }

            if (history[index].StartsWith(_historyPrefix, StringComparison.Ordinal))
            {
                _historyIndex = index;
                _commandLine.Text = _commandPrefix + history[index];
                _commandLine.SelectionStart = _commandLine.TextLength;
                return;
            }
        }
    }

    private void ExecuteFileCommand(ViExFileCommand command)
    {
        _commandFeedback = null;
        switch (command.Kind)
        {
            case ViExFileCommandKind.ReloadForce:
                if (_binaryMode) _binaryViewer.RefreshCurrentFile();
                else ReloadCurrentFileForce();
                break;
            case ViExFileCommandKind.EditAlternate:
                EditAlternateFile();
                break;
            case ViExFileCommandKind.Edit:
                EditFileFromCommand(command.Argument, force: false);
                break;
            case ViExFileCommandKind.EditForce:
                EditFileFromCommand(command.Argument, force: true);
                break;
            case ViExFileCommandKind.Quit:
                QuitFromCommand(force: false);
                break;
            case ViExFileCommandKind.QuitForce:
                QuitFromCommand(force: true);
                break;
            case ViExFileCommandKind.WriteCurrent:
            case ViExFileCommandKind.WriteCurrentForce:
                if (_binaryMode) _commandFeedback = "E382: Cannot write, 'buftype' option is set";
                else SaveDocument();
                break;
            case ViExFileCommandKind.WriteFile:
                WriteCopyFromCommand(command.Argument, force: false);
                break;
            case ViExFileCommandKind.WriteFileForce:
                WriteCopyFromCommand(command.Argument, force: true);
                break;
            case ViExFileCommandKind.WriteQuit:
                WriteQuitFromCommand(command.Argument, force: false);
                break;
            case ViExFileCommandKind.WriteQuitForce:
                WriteQuitFromCommand(command.Argument, force: true);
                break;
            case ViExFileCommandKind.Xit:
                XitFromCommand(command.Argument, force: false);
                break;
            case ViExFileCommandKind.XitForce:
                XitFromCommand(command.Argument, force: true);
                break;
            case ViExFileCommandKind.SaveAs:
                SaveAsFromCommand(command.Argument, force: false);
                break;
            case ViExFileCommandKind.SaveAsForce:
                SaveAsFromCommand(command.Argument, force: true);
                break;
        }
    }

    private void QuitFromCommand(bool force)
    {
        if (force)
        {
            _forceClose = true;
            Close();
            return;
        }
        if (_dirty)
        {
            _commandFeedback = "E37: No write since last change (add ! to override)";
            return;
        }
        _forceClose = true;
        Close();
    }

    private void EditFileFromCommand(string? rawPath, bool force)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            if (force) ReloadCurrentFileForce();
            else _commandFeedback = _filePath is null ? "E32: No file name" : Path.GetFileName(_filePath);
            return;
        }
        if (!force && _dirty)
        {
            _commandFeedback = "E37: No write since last change (add ! to override)";
            return;
        }

        var path = ResolveCommandPath(rawPath);
        if (path is null) return;
        if (_binaryMode)
        {
            SetBinaryMenuChecked(false);
            LeaveBinaryMode();
        }
        OpenFilePath(path, updateAlternate: true);
    }

    private void WriteQuitFromCommand(string? rawPath, bool force)
    {
        if (_binaryMode)
        {
            _commandFeedback = "E382: Cannot write binary view";
            return;
        }
        var saved = string.IsNullOrWhiteSpace(rawPath)
            ? SaveDocument()
            : WriteCopyFromCommand(rawPath, force);
        if (!saved) return;
        _forceClose = true;
        Close();
    }

    private void XitFromCommand(string? rawPath, bool force)
    {
        if (_binaryMode)
        {
            _forceClose = true;
            Close();
            return;
        }
        if (!_dirty && string.IsNullOrWhiteSpace(rawPath))
        {
            _forceClose = true;
            Close();
            return;
        }
        WriteQuitFromCommand(rawPath, force);
    }

    private void EndCommandInput()
    {
        _commandLine.Visible = false;
        _commandLine.Text = string.Empty;
        if (_binaryMode) _binaryViewer.FocusViewer();
        else _editor.Focus();
        UpdateStatus();
    }

    private void ShowCommandFeedback()
    {
        if (string.IsNullOrWhiteSpace(_commandFeedback)) return;
        _positionLabel.Text = _commandFeedback;
        _commandFeedback = null;
    }

    private void EditorOnSavePointLeft(object? sender, EventArgs e)
    {
        if (!_loading) SetDirty(true);
    }

    private void EditorOnSavePointReached(object? sender, EventArgs e)
    {
        if (!_loading) SetDirty(false);
    }

    private void SetDirty(bool dirty)
    {
        if (_dirty == dirty) return;
        _dirty = dirty;
        UpdateTitle();
    }

    private void NewDocument()
    {
        if (!ConfirmDiscardChanges()) return;
        if (_binaryMode)
        {
            SetBinaryMenuChecked(false);
            LeaveBinaryMode();
        }
        if (_filePath is not null) _alternateFilePath = _filePath;
        LoadTextIntoEditor(string.Empty);
        _filePath = null;
        _encoding = new UTF8Encoding(false);
        _newLine = "\r\n";
        UpdateTitle();
        UpdateStatus();
    }

    private void OpenDocument()
    {
        if (_binaryMode)
        {
            SetBinaryMenuChecked(false);
            LeaveBinaryMode();
        }
        if (!ConfirmDiscardChanges()) return;
        using var dialog = new OpenFileDialog
        {
            Filter = "テキスト/データファイル|*.txt;*.log;*.csv;*.md;*.json;*.xml;*.cs;*.py;*.js;*.ts;*.yaml;*.yml|すべてのファイル|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        OpenFilePath(dialog.FileName, updateAlternate: true);
    }

    private bool OpenFilePath(string path, bool updateAlternate)
    {
        try
        {
            var loaded = TextFileService.Load(path);
            var previousPath = _filePath;
            LoadTextIntoEditor(loaded.Text);
            if (updateAlternate && previousPath is not null && !PathsEqual(previousPath, path)) _alternateFilePath = previousPath;
            _filePath = Path.GetFullPath(path);
            _encoding = loaded.Encoding;
            _newLine = loaded.NewLine;
            UpdateTitle();
            UpdateStatus();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ファイルを開けません", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void ReloadCurrentFileForce()
    {
        if (_filePath is null)
        {
            _commandFeedback = "E32: No file name";
            return;
        }
        OpenFilePath(_filePath, updateAlternate: false);
    }

    private void EditAlternateFile()
    {
        if (_alternateFilePath is null)
        {
            _commandFeedback = "E23: No alternate file";
            return;
        }
        if (_dirty)
        {
            _commandFeedback = "E37: No write since last change (add ! to override)";
            return;
        }
        if (_binaryMode)
        {
            SetBinaryMenuChecked(false);
            LeaveBinaryMode();
        }
        OpenFilePath(_alternateFilePath, updateAlternate: true);
    }

    private void LoadTextIntoEditor(string text)
    {
        var restoreReadOnly = _referenceMode;
        _loading = true;
        try
        {
            _editor.ReadOnly = false;
            _editor.Text = text;
            _editor.GotoPosition(0);
            ResetUndoBaseline();
            _insertRepeatEdits.Clear();
            _capturingInsertRepeat = false;
        }
        finally
        {
            _editor.ReadOnly = restoreReadOnly;
            _loading = false;
        }
    }

    private bool SaveDocument() => _filePath is null ? SaveDocumentAs() : SaveTo(_filePath);

    private bool SaveDocumentAs()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "テキストファイル|*.txt|すべてのファイル|*.*",
            FileName = _filePath is null ? "untitled.txt" : Path.GetFileName(_filePath)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        return SaveTo(dialog.FileName);
    }

    private bool SaveAsFromCommand(string? rawPath, bool force)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            _commandFeedback = "E471: Argument required";
            return false;
        }
        var path = ResolveCommandPath(rawPath);
        if (path is null) return false;
        if (File.Exists(path) && !PathsEqual(_filePath, path) && !force)
        {
            _commandFeedback = $"E13: File exists (add ! to override): {path}";
            return false;
        }
        return SaveTo(path);
    }

    private bool WriteCopyFromCommand(string? rawPath, bool force)
    {
        if (_binaryMode)
        {
            _commandFeedback = "E382: Cannot write binary view";
            return false;
        }
        if (string.IsNullOrWhiteSpace(rawPath)) return SaveDocument();

        var path = ResolveCommandPath(rawPath);
        if (path is null) return false;
        if (PathsEqual(_filePath, path)) return SaveTo(path);
        if (File.Exists(path) && !force)
        {
            _commandFeedback = $"E13: File exists (add ! to override): {path}";
            return false;
        }

        try
        {
            TextFileService.Save(path, _editor.Text, _encoding);
            _alternateFilePath = path;
            _commandFeedback = $"written: {path}";
            return true;
        }
        catch (Exception ex)
        {
            _commandFeedback = ex.Message;
            return false;
        }
    }

    private string? ResolveCommandPath(string rawPath)
    {
        try
        {
            var pathText = rawPath.Trim();
            if (pathText.Length >= 2 && pathText[0] == '"' && pathText[^1] == '"') pathText = pathText[1..^1];
            return Path.GetFullPath(pathText);
        }
        catch (Exception ex)
        {
            _commandFeedback = ex.Message;
            return null;
        }
    }

    private bool SaveTo(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var previousPath = _filePath;
            TextFileService.Save(fullPath, _editor.Text, _encoding);
            if (previousPath is not null && !PathsEqual(previousPath, fullPath)) _alternateFilePath = previousPath;
            _filePath = fullPath;
            _editor.SetSavePoint();
            SetDirty(false);
            UpdateTitle();
            UpdateStatus();
            return true;
        }
        catch (Exception ex)
        {
            _commandFeedback = ex.Message;
            return false;
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (left is null || right is null) return false;
        try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(left, right, StringComparison.OrdinalIgnoreCase); }
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_dirty) return true;
        var result = MessageBox.Show(this, "変更内容を保存しますか？", "vi_text_editor", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return result switch { DialogResult.Yes => SaveDocument(), DialogResult.No => true, _ => false };
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_forceClose) return;
        if (!ConfirmDiscardChanges()) e.Cancel = true;
    }

    private void UpdateTitle()
    {
        if (_binaryMode && _binaryFilePath is not null)
        {
            Text = $"{Path.GetFileName(_binaryFilePath)} [BINARY] - vi_text_editor";
            return;
        }

        var name = _filePath is null ? "無題" : Path.GetFileName(_filePath);
        Text = $"{(_dirty ? "*" : string.Empty)}{name} - vi_text_editor";
    }

    private void UpdateStatus()
    {
        var ignoreCaseSuffix = ViOptions.Shared.IgnoreCase ? " IC" : string.Empty;
        if (_binaryMode)
        {
            _modeLabel.Text = _commandLine.Visible
                ? _commandPrefix == ':' ? "BINARY COMMAND" : "BINARY SEARCH"
                : "BINARY";
            _accessLabel.Text = "参照";
            _encodingLabel.Text = $"RAW bytes{ignoreCaseSuffix}";
            _eolLabel.Text = string.Empty;
            _positionLabel.Text = _binaryViewer.StatusText;
            return;
        }

        _modeLabel.Text = _commandLine.Visible
            ? "COMMAND"
            : _vi is null || _vi.Mode == EditorMode.Normal ? "NORMAL" : "INSERT";
        _accessLabel.Text = _referenceMode ? "参照" : "編集";
        _encodingLabel.Text = _encoding.WebName + ignoreCaseSuffix;
        _eolLabel.Text = _newLine switch { "\r\n" => "CRLF", "\n" => "LF", "\r" => "CR", _ => "EOL" };

        var position = Math.Clamp(_editor.CurrentPosition, 0, _editor.TextLength);
        var line = _editor.LineFromPosition(position) + 1;
        var column = _editor.GetColumn(position) + 1;
        _positionLabel.Text = $"Ln {line}, Col {column}";
    }

    private void ShowKeyBindings()
    {
        MessageBox.Show(this,
            "参照モードは既定でONです。モード > 参照モード で編集可能に切り替えられます。\n\n" +
            "NORMAL: h j k l / 0 ^ $ / w b（word）/ W B（WORD）/ Ctrl+F Ctrl+B / Ctrl+D Ctrl+U\n" +
            "REPEAT: .（直前の変更を繰り返す。p/P, x, dd, d*, c*, i/a/o/O+入力を対象）\n" +
            "検索: /文字列 / ?文字列 / n / N。:set ic / :set noic で大文字小文字判定を切替\n" +
            "COMMAND: :q / :q! / :w / :w! / :wq / :x / :e file / :e! / :e# / :saveas file\n" +
            "COMMAND: :set ic / :set noic / :set ic? / :%s/old/new/g / :2,5d\n" +
            "COMMAND: :y3 / :y a 3 / :5y a / :5,10y a / :pu a / :20pu a\n" +
            "履歴: : / / / ? の入力中に ↑/↓ で同種の履歴を再利用\n" +
            "レジスタ: COMMANDのyankとNORMALのyy/dd/dw/x/p/Pは同じ無名レジスタを共有\n" +
            "バイナリ: :set ic / :set noic、j/k、Ctrl+F/B、Ctrl+D/U、gg/G、/ ? n N\n" +
            "バイナリRAW検索: /hex:4D 5A の形式（RAW HEXはignorecase対象外）\n" +
            "その他: gg G / x / yy / p P / u / Ctrl+R\n" +
            "INSERT: i / a / o / O、EscでNORMALへ戻る",
            "viキーバインド", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
