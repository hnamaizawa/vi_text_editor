using System.Text;
using ScintillaNET;
using ViTextEditor.Core.Editor;
using ViTextEditor.Core.IO;

namespace ViTextEditor;

public sealed class MainForm : Form
{
    private readonly Scintilla _editor = new();
    private readonly TextBox _commandLine = new();
    private readonly ToolStripStatusLabel _modeLabel = new();
    private readonly ToolStripStatusLabel _accessLabel = new();
    private readonly ToolStripStatusLabel _encodingLabel = new();
    private readonly ToolStripStatusLabel _eolLabel = new();
    private readonly ToolStripStatusLabel _positionLabel = new();
    private readonly ViKeyProcessor _vi;
    private readonly ViNavigationProcessor _navigation;
    private readonly ViSearchProcessor _search;
    private readonly ViCommandProcessor _commands;
    private ToolStripMenuItem? _referenceModeMenuItem;
    private ToolStripMenuItem? _undoMenuItem;
    private ToolStripMenuItem? _redoMenuItem;

    private string? _filePath;
    private string? _alternateFilePath;
    private Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private string _newLine = "\r\n";
    private bool _dirty;
    private bool _loading;
    private bool _referenceMode = true;
    private bool _forceClose;
    private char _commandPrefix;

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
        _editor.MouseUp += (_, _) => UpdateStatus();
        _editor.TextChanged += (_, _) => UpdateStatus();
        _editor.SavePointLeft += EditorOnSavePointLeft;
        _editor.SavePointReached += EditorOnSavePointReached;

        ConfigureCommandLine();

        Controls.Add(_editor);
        Controls.Add(_commandLine);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;

        var adapter = new ScintillaEditorAdapter(_editor);
        _vi = new ViKeyProcessor(adapter);
        _navigation = new ViNavigationProcessor(adapter);
        _search = new ViSearchProcessor(adapter);
        _commands = new ViCommandProcessor(adapter);
        _vi.ModeChanged += (_, _) =>
        {
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

    private void ConfigureEditorAppearance()
    {
        var fontName = SelectMonospacedJapaneseFont();
        _editor.Styles[Style.Default].Font = fontName;
        _editor.Styles[Style.Default].SizeF = 11f;
        _editor.StyleClearAll();
        _editor.CaretWidth = 3;
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
            if (installed.Contains(candidate))
            {
                return candidate;
            }
        }

        return "Consolas";
    }

    private void ApplyCaretStyleForMode()
    {
        if (_vi is null)
        {
            return;
        }

        _editor.CaretStyle = _vi.Mode == EditorMode.Normal ? CaretStyle.Block : CaretStyle.Line;
        _editor.CaretWidth = 3;
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("ファイル(&F)");
        file.DropDownItems.Add(new ToolStripMenuItem("新規(&N)", null, (_, _) => NewDocument(), Keys.Control | Keys.N));
        file.DropDownItems.Add(new ToolStripMenuItem("開く(&O)...", null, (_, _) => OpenDocument(), Keys.Control | Keys.O));
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

        var help = new ToolStripMenuItem("ヘルプ(&H)");
        help.DropDownItems.Add(new ToolStripMenuItem("viキーバインド", null, (_, _) => ShowKeyBindings()));
        help.DropDownItems.Add(new ToolStripMenuItem("バージョン情報", null, (_, _) => MessageBox.Show(this, "vi_text_editor v0.1.8", "バージョン情報", MessageBoxButtons.OK, MessageBoxIcon.Information)));

        menu.Items.AddRange([file, edit, mode, help]);
        return menu;
    }

    private StatusStrip BuildStatusBar()
    {
        var status = new StatusStrip();
        _modeLabel.AutoSize = false;
        _modeLabel.Width = 90;
        _accessLabel.AutoSize = false;
        _accessLabel.Width = 90;
        _encodingLabel.Spring = true;
        _encodingLabel.TextAlign = ContentAlignment.MiddleRight;
        status.Items.AddRange([_modeLabel, _accessLabel, _encodingLabel, _eolLabel, _positionLabel]);
        return status;
    }

    private void ApplyReferenceMode()
    {
        if (_referenceMode && _vi is not null)
        {
            _vi.Handle("Esc");
        }
        _editor.ReadOnly = _referenceMode;
        if (_undoMenuItem is not null) _undoMenuItem.Enabled = !_referenceMode;
        if (_redoMenuItem is not null) _redoMenuItem.Enabled = !_referenceMode;
        UpdateStatus();
    }

    private void ResetUndoBaseline()
    {
        _editor.EmptyUndoBuffer();
        SetDirty(false);
    }

    private void EditorOnKeyDown(object? sender, KeyEventArgs e)
    {
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
        if (_vi.Mode != EditorMode.Normal || _commandLine.Visible)
        {
            return;
        }

        var token = e.KeyChar switch
        {
            '^' => "^",
            ':' => ":",
            '/' => "/",
            '?' => "?",
            _ => null
        };

        if (token is ":" or "/" or "?")
        {
            BeginCommandInput(token[0]);
            e.Handled = true;
            return;
        }

        if (token == "^")
        {
            _vi.Handle(token);
            e.Handled = true;
            return;
        }

        // NORMALモードでは未対応の印字文字を本文へ挿入しない。
        if (!char.IsControl(e.KeyChar))
        {
            e.Handled = true;
        }
    }

    private static bool IsMutatingViToken(string token) => token is "i" or "a" or "o" or "O" or "x" or "d" or "D" or "c" or "p" or "P" or "u" or "Ctrl+r";

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

        if (e.KeyCode == Keys.Back && _commandLine.SelectionStart <= 1 && _commandLine.SelectionLength == 0)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        var value = _commandLine.Text.Length > 1 ? _commandLine.Text[1..] : string.Empty;

        if (_commandPrefix == ':' && ViExFileCommandParser.TryParse(value, out var fileCommand))
        {
            EndCommandInput();
            ExecuteFileCommand(fileCommand);
            UpdateStatus();
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

        EndCommandInput();
        if (acted)
        {
            _editor.ScrollCaret();
        }
        UpdateStatus();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void ExecuteFileCommand(ViExFileCommand command)
    {
        switch (command.Kind)
        {
            case ViExFileCommandKind.ReloadForce:
                ReloadCurrentFileForce();
                break;
            case ViExFileCommandKind.EditAlternate:
                EditAlternateFile();
                break;
            case ViExFileCommandKind.QuitForce:
                _forceClose = true;
                Close();
                break;
            case ViExFileCommandKind.WriteCurrent:
                SaveDocument();
                break;
            case ViExFileCommandKind.WriteAs:
                SaveAsFromCommand(command.Argument);
                break;
        }
    }

    private void EndCommandInput()
    {
        _commandLine.Visible = false;
        _commandLine.Text = string.Empty;
        _editor.Focus();
        UpdateStatus();
    }

    private void EditorOnSavePointLeft(object? sender, EventArgs e)
    {
        if (!_loading)
        {
            SetDirty(true);
        }
    }

    private void EditorOnSavePointReached(object? sender, EventArgs e)
    {
        if (!_loading)
        {
            SetDirty(false);
        }
    }

    private void SetDirty(bool dirty)
    {
        if (_dirty == dirty)
        {
            return;
        }

        _dirty = dirty;
        UpdateTitle();
    }

    private void NewDocument()
    {
        if (!ConfirmDiscardChanges()) return;
        if (_filePath is not null)
        {
            _alternateFilePath = _filePath;
        }
        LoadTextIntoEditor(string.Empty);
        _filePath = null;
        _encoding = new UTF8Encoding(false);
        _newLine = "\r\n";
        UpdateTitle();
        UpdateStatus();
    }

    private void OpenDocument()
    {
        if (!ConfirmDiscardChanges()) return;
        using var dialog = new OpenFileDialog
        {
            Filter = "テキストファイル|*.txt;*.log;*.csv;*.md;*.json;*.xml;*.cs;*.py;*.js;*.ts;*.yaml;*.yml|すべてのファイル|*.*",
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
            if (updateAlternate && previousPath is not null && !PathsEqual(previousPath, path))
            {
                _alternateFilePath = previousPath;
            }
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
            MessageBox.Show(this, "再読み込みできる現在ファイルがありません。", "vi_text_editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        OpenFilePath(_filePath, updateAlternate: false);
    }

    private void EditAlternateFile()
    {
        if (_alternateFilePath is null)
        {
            MessageBox.Show(this, "副ファイル（alternate file）がありません。", "vi_text_editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!ConfirmDiscardChanges())
        {
            return;
        }

        var target = _alternateFilePath;
        OpenFilePath(target, updateAlternate: true);
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

    private bool SaveAsFromCommand(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return SaveDocument();
        }

        try
        {
            var pathText = rawPath.Trim();
            if (pathText.Length >= 2 && pathText[0] == '"' && pathText[^1] == '"')
            {
                pathText = pathText[1..^1];
            }

            var path = Path.GetFullPath(pathText);
            if (File.Exists(path) && !PathsEqual(_filePath, path))
            {
                var overwrite = MessageBox.Show(this, $"既存のファイルを上書きしますか？\n{path}", "vi_text_editor", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (overwrite != DialogResult.Yes)
                {
                    return false;
                }
            }

            return SaveTo(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存できません", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private bool SaveTo(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var previousPath = _filePath;
            TextFileService.Save(fullPath, _editor.Text, _encoding);
            if (previousPath is not null && !PathsEqual(previousPath, fullPath))
            {
                _alternateFilePath = previousPath;
            }
            _filePath = fullPath;
            _editor.SetSavePoint();
            SetDirty(false);
            UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存できません", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_dirty) return true;
        var result = MessageBox.Show(this, "変更内容を保存しますか？", "vi_text_editor", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return result switch { DialogResult.Yes => SaveDocument(), DialogResult.No => true, _ => false };
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_forceClose)
        {
            return;
        }

        if (!ConfirmDiscardChanges()) e.Cancel = true;
    }

    private void UpdateTitle()
    {
        var name = _filePath is null ? "無題" : Path.GetFileName(_filePath);
        Text = $"{(_dirty ? "*" : string.Empty)}{name} - vi_text_editor";
    }

    private void UpdateStatus()
    {
        _modeLabel.Text = _commandLine.Visible
            ? "COMMAND"
            : _vi is null || _vi.Mode == EditorMode.Normal ? "NORMAL" : "INSERT";
        _accessLabel.Text = _referenceMode ? "参照" : "編集";
        _encodingLabel.Text = _encoding.WebName;
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
            "NORMAL: h j k l / 0 ^ $ / w b（word）/ W B（WORD）/ Ctrl+F Ctrl+B（1画面）/ Ctrl+D Ctrl+U（半画面）\n" +
            "CHANGE: cw / ce（word末尾まで変更）/ cW / cE（WORD末尾まで変更）/ c$（行末まで変更）\n" +
            "DELETE: dw / de（word単位）/ dW / dE（WORD単位）/ d$ / D（行末まで）/ dd（行削除）\n" +
            "検索: /文字列 / ?文字列 / n（同方向）/ N（逆方向）\n" +
            "COMMAND: :e!（変更破棄で再読込）/ :e#（副ファイル）/ :q!（強制終了）/ :w [ファイル名]（保存/別名保存）\n" +
            "COMMAND: :120 / :$ / :5y a / :5,10y a / :pu a / :20pu a\n" +
            "その他: gg G / x / yy / p P / u / Ctrl+R\n" +
            "Undoでsave pointまで戻るとタイトルの * は自動的に消えます。\n" +
            "INSERT: i / a / o / O、EscでNORMALへ戻る",
            "viキーバインド", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
