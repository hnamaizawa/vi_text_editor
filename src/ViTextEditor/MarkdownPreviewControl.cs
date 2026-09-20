using Markdig;

namespace ViTextEditor;

internal sealed class MarkdownPreviewControl : UserControl
{
    private readonly WebBrowser _browser = new();
    private readonly ToolStrip _toolbar = new();
    private readonly ToolStripLabel _sourceLabel = new();
    private readonly Func<string> _markdownProvider;
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public MarkdownPreviewControl(string sourceName, Func<string> markdownProvider)
    {
        _markdownProvider = markdownProvider;
        Dock = DockStyle.Fill;

        _sourceLabel.Text = sourceName;
        _toolbar.GripStyle = ToolStripGripStyle.Hidden;
        _toolbar.Items.Add(new ToolStripButton("更新", null, (_, _) => RefreshPreview()));
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(_sourceLabel);
        _toolbar.Dock = DockStyle.Top;

        _browser.Dock = DockStyle.Fill;
        _browser.ScriptErrorsSuppressed = true;
        _browser.AllowWebBrowserDrop = false;
        _browser.WebBrowserShortcutsEnabled = true;

        Controls.Add(_browser);
        Controls.Add(_toolbar);
        RefreshPreview();
    }

    public void RefreshPreview()
    {
        string markdown;
        try { markdown = _markdownProvider(); }
        catch { markdown = string.Empty; }
        var body = Markdown.ToHtml(markdown, _pipeline);
        _browser.DocumentText = BuildHtml(body);
    }

    private static string BuildHtml(string body) => $$"""
<!doctype html>
<html><head><meta charset="utf-8">
<style>
body{font-family:"Yu Gothic UI","Meiryo",sans-serif;line-height:1.65;margin:0 auto;padding:32px;max-width:980px;color:#222;background:#fff}
pre,code{font-family:"BIZ UDGothic","MS Gothic",Consolas,monospace;background:#f5f5f5}pre{padding:14px;overflow:auto}code{padding:2px 4px}
table{border-collapse:collapse}th,td{border:1px solid #ccc;padding:6px 10px}blockquote{border-left:4px solid #bbb;margin-left:0;padding-left:14px;color:#555}
img{max-width:100%}a{color:#0969da}h1,h2{border-bottom:1px solid #ddd;padding-bottom:.3em}
</style></head><body>{{body}}</body></html>
""";
}
