using System.Text;
using ScintillaNET;
using ViTextEditor.Core.Editor;
using ViTextEditor.Core.IO;

namespace ViTextEditor;

public sealed class MainForm : Form
{
    private readonly Scintilla _editor = new();
    private readonly ToolStripStatusLabel _modeLabel = new();
    private readonly ToolStripStatusLabel _accessLabel = new();
    private readonly ToolStripStatusLabel _encodingLabel = new();
    private readonly ToolStripStatusLabel _eolLabel = new();
    private readonly ToolStripStatusLabel _positionLabel = new();
    private readonly ViKeyProcessor _vi;
    private ToolStripMenuItem? _referenceModeMenuItem;
    private ToolStripMenuItem? _undoMenuItem;
    private ToolStripMenuItem? _redoMenuItem;

    private string? _filePath;
    private Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private string _newLine = "\r\n";
    private bool _dirty;
    private bool _loading;
    private bool _referenceMode = true;

    public MainForm()
    {
        Text = "vi_text_editor";
        Width = 1100;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;

        var menu = BuildMenu();
        var status = BuildStatusBar();

        _editor.Dock = DockStyle.Fill;
        _editor.Font = new Font("Consolas", 11f);
        _editor.Margins[0].Type = MarginType.Number;
        _editor.Margins[0].Width = 48;
        _editor.KeyDown += EditorOnKeyDown;
        _editor.KeyUp += (_, _) => UpdateStatus();
        _editor.MouseUp += (_, _) => UpdateStatus();
        _editor.TextChanged += EditorOnTextChanged;

        Controls.Add(_editor);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;

        var adapter = new ScintillaEditorAdapter(_editor);
        _vi = new ViKeyProcessor(adapter);
        _vi.ModeChanged += (_, _) => UpdateStatus();

        FormClosing += OnFormClosing;
        ApplyReferenceMode();
        ResetUndoBaseline();
        UpdateTitle();
        UpdateStatus();
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
        help.DropDownItems.Add(new ToolStripMenuItem("バージョン情報", null, (_, _) => MessageBox.Show(this, "vi_text_editor v0.1.3", "バージョン情報", MessageBoxButtons.OK, MessageBoxIcon.Information)));

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

        if (_vi.Handle(token))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            UpdateStatus();
        }
    }

    private static bool IsMutatingViToken(string token) => token is "i" or "a" or "o" or "O" or "x" or "d" or "p" or "P" or "u" or "Ctrl+r";

    private static string? ToViToken(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) return "Esc";
        if (e.Control && e.KeyCode == Keys.R) return "Ctrl+r";
        if (e.Control || e.Alt) return null;
        if (e.Shift)
        {
            return e.KeyCode switch
            {
                Keys.G => "G", Keys.O => "O", Keys.P => "P", Keys.D6 => "^", Keys.D4 => "$", _ => null
            };
        }
        return e.KeyCode switch
        {
            Keys.I => "i", Keys.A => "a", Keys.O => "o", Keys.H => "h", Keys.J => "j", Keys.K => "k", Keys.L => "l",
            Keys.W => "w", Keys.B => "b", Keys.E => "e", Keys.G => "g", Keys.D => "d", Keys.Y => "y", Keys.X => "x",
            Keys.P => "p", Keys.U => "u", Keys.D0 => "0", _ => null
        };
    }

    private void EditorOnTextChanged(object? sender, EventArgs e)
    {
        if (!_loading)
        {
            _dirty = true;
            UpdateTitle();
        }
        UpdateStatus();
    }

    private void NewDocument()
    {
        if (!ConfirmDiscardChanges()) return;
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
        try
        {
            var loaded = TextFileService.Load(dialog.FileName);
            LoadTextIntoEditor(loaded.Text);
            _filePath = dialog.FileName;
            _encoding = loaded.Encoding;
            _newLine = loaded.NewLine;
            UpdateTitle();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ファイルを開けません", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
            _dirty = false;
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

    private bool SaveTo(string path)
    {
        try
        {
            TextFileService.Save(path, _editor.Text, _encoding);
            _filePath = path;
            _dirty = false;
            UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "保存できません", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
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
        if (!ConfirmDiscardChanges()) e.Cancel = true;
    }

    private void UpdateTitle()
    {
        var name = _filePath is null ? "無題" : Path.GetFileName(_filePath);
        Text = $"{(_dirty ? "*" : string.Empty)}{name} - vi_text_editor";
    }

    private void UpdateStatus()
    {
        _modeLabel.Text = _vi is null || _vi.Mode == EditorMode.Normal ? "NORMAL" : "INSERT";
        _accessLabel.Text = _referenceMode ? "参照" : "編集";
        _encodingLabel.Text = _encoding.WebName;
        _eolLabel.Text = _newLine switch { "\r\n" => "CRLF", "\n" => "LF", "\r" => "CR", _ => "EOL" };
        var text = _editor.Text;
        var position = Math.Clamp(_editor.CurrentPosition, 0, text.Length);
        var before = position == 0 ? string.Empty : text[..Math.Min(position, text.Length)];
        var line = before.Count(c => c == '\n') + 1;
        var lastBreak = before.LastIndexOf('\n');
        var column = position - (lastBreak + 1) + 1;
        _positionLabel.Text = $"Ln {line}, Col {column}";
    }

    private void ShowKeyBindings()
    {
        MessageBox.Show(this,
            "参照モードは既定でONです。モード > 参照モード で編集可能に切り替えられます。\n\n" +
            "NORMAL: h j k l / w b e / 0 ^ $ / gg G / x / dd / yy / p P / u / Ctrl+R\n" +
            "INSERT: i / a / o / O、EscでNORMALへ戻る",
            "viキーバインド", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
