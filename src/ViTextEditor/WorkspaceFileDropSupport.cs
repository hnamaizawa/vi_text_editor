namespace ViTextEditor;

internal sealed class WorkspaceFileDropSupport : IDisposable
{
    private readonly EditorWorkspaceForm _workspace;
    private readonly HashSet<Control> _registered = [];
    private bool _disposed;

    private WorkspaceFileDropSupport(EditorWorkspaceForm workspace)
    {
        _workspace = workspace;
        RegisterRecursively(workspace);
    }

    public static WorkspaceFileDropSupport Attach(EditorWorkspaceForm workspace) => new(workspace);

    private void RegisterRecursively(Control control)
    {
        if (!_registered.Add(control)) return;

        control.ControlAdded += ControlOnControlAdded;
        try
        {
            control.AllowDrop = true;
            control.DragEnter += ControlOnDragEnter;
            control.DragDrop += ControlOnDragDrop;
        }
        catch (NotSupportedException)
        {
            // Some hosted/ActiveX controls do not expose OLE drop registration.
            // Their registered parent controls continue to provide the drop target.
        }

        foreach (Control child in control.Controls)
            RegisterRecursively(child);
    }

    private void ControlOnControlAdded(object? sender, ControlEventArgs e) => RegisterRecursively(e.Control);

    private static string[] GetDroppedFiles(IDataObject? data)
    {
        if (data?.GetDataPresent(DataFormats.FileDrop) != true) return [];
        return (data.GetData(DataFormats.FileDrop) as string[] ?? [])
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void ControlOnDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = GetDroppedFiles(e.Data).Length > 0
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void ControlOnDragDrop(object? sender, DragEventArgs e)
    {
        var files = GetDroppedFiles(e.Data);
        if (files.Length == 0) return;

        foreach (var file in files)
            _workspace.OpenPath(file, select: true);

        if (_workspace.WindowState == FormWindowState.Minimized)
            _workspace.WindowState = FormWindowState.Normal;
        _workspace.BringToFront();
        _workspace.Activate();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var control in _registered.ToArray())
        {
            control.ControlAdded -= ControlOnControlAdded;
            control.DragEnter -= ControlOnDragEnter;
            control.DragDrop -= ControlOnDragDrop;
        }
        _registered.Clear();
    }
}
