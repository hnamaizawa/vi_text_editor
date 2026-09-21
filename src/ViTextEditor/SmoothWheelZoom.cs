using ScintillaNET;

namespace ViTextEditor;

internal sealed class SmoothWheelZoomFilter : IMessageFilter, IDisposable
{
    private const int WmMouseWheel = 0x020A;
    private const int MkControl = 0x0008;

    private readonly Control _target;
    private readonly Action<int> _applySteps;
    private readonly System.Windows.Forms.Timer _timer;
    private int _pendingDelta;
    private bool _disposed;

    private SmoothWheelZoomFilter(Control target, Action<int> applySteps)
    {
        _target = target;
        _applySteps = applySteps;
        _timer = new System.Windows.Forms.Timer { Interval = 45 };
        _timer.Tick += TimerOnTick;
        Application.AddMessageFilter(this);
    }

    public static IDisposable Attach(Control target, Action<int> applySteps) =>
        new SmoothWheelZoomFilter(target, applySteps);

    public bool PreFilterMessage(ref Message m)
    {
        if (_disposed || m.Msg != WmMouseWheel || _target.IsDisposed || !_target.Visible || !_target.IsHandleCreated)
            return false;

        var raw = m.WParam.ToInt64();
        var keyState = (int)(raw & 0xFFFF);
        if ((keyState & MkControl) == 0) return false;

        Rectangle bounds;
        try { bounds = _target.RectangleToScreen(_target.ClientRectangle); }
        catch { return false; }
        if (!bounds.Contains(Cursor.Position)) return false;

        var delta = unchecked((short)((raw >> 16) & 0xFFFF));
        if (delta == 0) return true;

        _pendingDelta += delta;
        _timer.Stop();
        _timer.Start();

        // Consume the native Ctrl+wheel handling. A single debounced zoom update is
        // applied after the wheel burst, avoiding repeated reflow/repaint "waving".
        return true;
    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        if (_disposed || _target.IsDisposed)
        {
            _pendingDelta = 0;
            return;
        }

        var delta = _pendingDelta;
        _pendingDelta = 0;
        if (delta == 0) return;

        var steps = delta / 120;
        if (steps == 0) steps = Math.Sign(delta);
        _applySteps(steps);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Application.RemoveMessageFilter(this);
        _timer.Stop();
        _timer.Dispose();
    }
}

internal static class SmoothEditorZoom
{
    private const int SciSetZoom = 2373;
    private const int SciGetZoom = 2374;
    private const int SciGetFirstVisibleLine = 2152;
    private const int SciLineScroll = 2168;

    public static IDisposable Attach(Scintilla editor)
    {
        return SmoothWheelZoomFilter.Attach(editor, steps => Apply(editor, steps));
    }

    private static void Apply(Scintilla editor, int steps)
    {
        if (editor.IsDisposed || steps == 0) return;

        var current = editor.DirectMessage(SciGetZoom, IntPtr.Zero).ToInt32();
        var target = Math.Clamp(current + steps, -10, 20);
        if (target == current) return;

        // Keep the same top document line anchored while the font size changes.
        // This avoids the visible vertical oscillation of repeated native zoom events.
        var firstVisible = editor.DirectMessage(SciGetFirstVisibleLine, IntPtr.Zero).ToInt32();
        editor.SuspendLayout();
        try
        {
            editor.DirectMessage(SciSetZoom, new IntPtr(target));
            var after = editor.DirectMessage(SciGetFirstVisibleLine, IntPtr.Zero).ToInt32();
            var lineDelta = firstVisible - after;
            if (lineDelta != 0)
                editor.DirectMessage(SciLineScroll, IntPtr.Zero, new IntPtr(lineDelta));
            editor.ScrollCaret();
        }
        finally
        {
            editor.ResumeLayout(false);
            editor.Invalidate();
        }
    }
}
