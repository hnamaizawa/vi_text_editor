using Markdig;
using ViTextEditor.Core.Editor;

namespace ViTextEditor;

internal sealed class MarkdownPreviewControl : UserControl
{
    private readonly WebBrowser _browser = new();
    private readonly ToolStrip _toolbar = new();
    private readonly ToolStripLabel _sourceLabel = new();
    private readonly ToolStripLabel _statusLabel = new();
    private readonly TextBox _command = new();
    private readonly Func<string> _markdownProvider;
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private readonly ViOptions _options = ViOptions.Shared;
    private IDisposable? _zoomFilter;
    private bool _pendingG;
    private string? _lastSearch;
    private bool _lastSearchForward = true;
    private bool _lastSearchIgnoreCase;
    private char _commandPrefix;
    private int _zoomPercent = 100;

    public MarkdownPreviewControl(string sourceName, Func<string> markdownProvider)
    {
        _markdownProvider = markdownProvider;
        Dock = DockStyle.Fill;

        _sourceLabel.Text = sourceName;
        _statusLabel.Alignment = ToolStripItemAlignment.Right;
        _toolbar.GripStyle = ToolStripGripStyle.Hidden;
        _toolbar.Items.Add(new ToolStripButton("更新", null, (_, _) => RefreshPreview()));
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(_sourceLabel);
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(new ToolStripLabel("vi: j/k  Ctrl+F/B/D/U  gg/G  / ? n/N"));
        _toolbar.Items.Add(_statusLabel);
        _toolbar.Dock = DockStyle.Top;

        _command.Dock = DockStyle.Bottom;
        _command.Visible = false;
        _command.BorderStyle = BorderStyle.FixedSingle;
        _command.Font = new Font("Consolas", 10f);
        _command.KeyDown += CommandOnKeyDown;

        _browser.Dock = DockStyle.Fill;
        _browser.ScriptErrorsSuppressed = true;
        _browser.AllowWebBrowserDrop = false;
        // The preview is intentionally vi-driven. Disable IE/WebBrowser shortcuts
        // such as Ctrl+F so the workspace can route them to vi page movement.
        _browser.WebBrowserShortcutsEnabled = false;
        _browser.DocumentCompleted += (_, _) => UpdateStatus();

        Controls.Add(_browser);
        Controls.Add(_command);
        Controls.Add(_toolbar);

        _zoomFilter = SmoothWheelZoomFilter.Attach(_browser, ApplyZoomSteps);
        RefreshPreview();
        UpdateStatus();
    }

    public void FocusViewer() => _browser.Focus();

    public bool HandleKey(Keys keyData)
    {
        if (_command.Visible) return false;

        var keyCode = keyData & Keys.KeyCode;
        var control = (keyData & Keys.Control) == Keys.Control;
        var shift = (keyData & Keys.Shift) == Keys.Shift;
        var alt = (keyData & Keys.Alt) == Keys.Alt;
        if (alt) return false;

        if (control)
        {
            switch (keyCode)
            {
                case Keys.F: ScrollPage(1.0); return true;
                case Keys.B: ScrollPage(-1.0); return true;
                case Keys.D: ScrollPage(0.5); return true;
                case Keys.U: ScrollPage(-0.5); return true;
            }
            return false;
        }

        if (keyCode == Keys.G)
        {
            if (shift)
            {
                _pendingG = false;
                InvokeScript("viScrollBottom");
            }
            else if (_pendingG)
            {
                _pendingG = false;
                InvokeScript("viScrollTop");
            }
            else
            {
                _pendingG = true;
            }
            return true;
        }

        _pendingG = false;
        if (keyCode == Keys.J) { ScrollBy(44); return true; }
        if (keyCode == Keys.K) { ScrollBy(-44); return true; }
        if (keyCode == Keys.N) { RepeatSearch(reverse: shift); return true; }

        // '/' and '?' share the same physical key on common JIS/US layouts.
        if (keyCode == Keys.OemQuestion)
        {
            BeginCommand(shift ? '?' : '/');
            return true;
        }

        // ':' is optional in the viewer, but useful for :set ic / :set noic.
        if (shift && keyCode == Keys.OemSemicolon)
        {
            BeginCommand(':');
            return true;
        }

        return false;
    }

    public void RefreshPreview()
    {
        string markdown;
        try { markdown = _markdownProvider(); }
        catch { markdown = string.Empty; }
        var body = Markdown.ToHtml(markdown, _pipeline);
        _browser.DocumentText = BuildHtml(body);
        _lastSearch = null;
        _pendingG = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _zoomFilter?.Dispose();
            _zoomFilter = null;
        }
        base.Dispose(disposing);
    }

    private void ScrollBy(int pixels) => InvokeScript("viScrollBy", pixels);

    private void ScrollPage(double factor) => InvokeScript("viScrollPage", factor);

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
        if (e.KeyCode == Keys.Escape)
        {
            EndCommand();
            Consume(e);
            return;
        }
        if (e.KeyCode == Keys.Back && _command.SelectionStart <= 1 && _command.SelectionLength == 0)
        {
            Consume(e);
            return;
        }
        if (e.KeyCode != Keys.Enter) return;

        var value = _command.Text.Length > 1 ? _command.Text[1..] : string.Empty;
        if (_commandPrefix == ':')
        {
            if (_options.TryExecuteSet(value, out var message))
                _statusLabel.Text = message ?? string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(value))
        {
            Search(value, _commandPrefix == '/');
        }
        EndCommand();
        Consume(e);
    }

    private void EndCommand()
    {
        _command.Visible = false;
        _command.Text = string.Empty;
        _browser.Focus();
        UpdateStatus();
    }

    private bool Search(string query, bool forward)
    {
        var forceIgnore = query.Contains("\\c", StringComparison.Ordinal);
        var forceCase = query.Contains("\\C", StringComparison.Ordinal);
        query = query.Replace("\\c", string.Empty, StringComparison.Ordinal)
                     .Replace("\\C", string.Empty, StringComparison.Ordinal);
        if (query.Length == 0) return false;

        var ignoreCase = forceCase ? false : forceIgnore || _options.IgnoreCase;
        var found = InvokeFind(query, forward, ignoreCase);
        if (!found)
        {
            _statusLabel.Text = $"Pattern not found: {query}";
            return false;
        }

        _lastSearch = query;
        _lastSearchForward = forward;
        _lastSearchIgnoreCase = ignoreCase;
        UpdateStatus();
        return true;
    }

    private void RepeatSearch(bool reverse)
    {
        if (string.IsNullOrEmpty(_lastSearch)) return;
        var forward = reverse ? !_lastSearchForward : _lastSearchForward;
        if (!InvokeFind(_lastSearch, forward, _lastSearchIgnoreCase))
            _statusLabel.Text = $"Pattern not found: {_lastSearch}";
    }

    private bool InvokeFind(string query, bool forward, bool ignoreCase)
    {
        var result = InvokeScript("viFind", query, !forward, ignoreCase);
        return result is bool value && value;
    }

    private object? InvokeScript(string name, params object[] args)
    {
        try { return _browser.Document?.InvokeScript(name, args); }
        catch { return null; }
    }

    private void ApplyZoomSteps(int steps)
    {
        if (steps == 0) return;
        _zoomPercent = Math.Clamp(_zoomPercent + steps * 10, 50, 200);
        InvokeScript("viSetZoom", _zoomPercent);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        _statusLabel.Text = $"{(_options.IgnoreCase ? "ignorecase" : "case-sensitive")}  {_zoomPercent}%";
    }

    private static void Consume(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private static string BuildHtml(string body) => $$"""
<!doctype html>
<html><head><meta charset="utf-8">
<style>
html{scroll-behavior:auto}
body{font-family:"Yu Gothic UI","Meiryo",sans-serif;line-height:1.65;margin:0 auto;padding:32px;max-width:980px;color:#222;background:#fff;transform-origin:top left}
pre,code{font-family:"BIZ UDGothic","MS Gothic",Consolas,monospace;background:#f5f5f5}pre{padding:14px;overflow:auto}code{padding:2px 4px}
table{border-collapse:collapse}th,td{border:1px solid #ccc;padding:6px 10px}blockquote{border-left:4px solid #bbb;margin-left:0;padding-left:14px;color:#555}
img{max-width:100%}a{color:#0969da}h1,h2{border-bottom:1px solid #ddd;padding-bottom:.3em}
</style>
<script>
function viScrollBy(px){window.scrollBy(0,px);}
function viScrollPage(factor){window.scrollBy(0,Math.round((window.innerHeight||600)*factor));}
function viScrollTop(){window.scrollTo(0,0);}
function viScrollBottom(){window.scrollTo(0,Math.max(document.body.scrollHeight,document.documentElement.scrollHeight));}
function viFind(text,backwards,ignoreCase){
  if(!text||!document.body||!document.body.createTextRange)return false;
  try{
    var range=document.body.createTextRange();
    var sel=document.selection?document.selection.createRange():null;
    if(sel&&sel.text){
      if(backwards)range.setEndPoint('EndToStart',sel);
      else range.setEndPoint('StartToEnd',sel);
    }
    var flags=ignoreCase?0:4;
    var count=backwards?-1073741824:1073741824;
    if(!range.findText(text,count,flags)){
      range=document.body.createTextRange();
      if(!range.findText(text,count,flags))return false;
    }
    range.select();
    range.scrollIntoView(false);
    return true;
  }catch(e){return false;}
}
function viSetZoom(pct){
  try{
    var doc=document.documentElement,body=document.body;
    var top=doc.scrollTop||body.scrollTop||0;
    var height=Math.max(body.scrollHeight,doc.scrollHeight,1);
    var ratio=top/height;
    body.style.zoom=pct+'%';
    var newHeight=Math.max(body.scrollHeight,doc.scrollHeight,1);
    window.scrollTo(0,Math.round(ratio*newHeight));
  }catch(e){}
}
</script></head><body>{{body}}</body></html>
""";
}
