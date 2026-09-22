using System.Runtime.InteropServices;
using System.Text;
using ScintillaNET;
using RuntimeArchitecture = System.Runtime.InteropServices.Architecture;

namespace ViTextEditor;

internal static class Program
{
    private const string StartupSmokeTestArgument = "--startup-smoke-test";
    private const string StartupOpenSmokeTestArgument = "--startup-open-smoke-test";
    private const string SingleInstanceSmokeHostArgument = "--single-instance-smoke-host";

    [STAThread]
    private static int Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var startupSmokeTest = args.Any(arg => string.Equals(arg, StartupSmokeTestArgument, StringComparison.OrdinalIgnoreCase));
        var startupOpenSmokeTest = args.Any(arg => string.Equals(arg, StartupOpenSmokeTestArgument, StringComparison.OrdinalIgnoreCase));
        var singleInstanceSmokeHost = Array.FindIndex(args, arg =>
            string.Equals(arg, SingleInstanceSmokeHostArgument, StringComparison.OrdinalIgnoreCase));

        // Packaging smoke tests intentionally bypass normal single-instance routing.
        // They need to exercise the packaged executable in isolation and terminate.
        if (startupSmokeTest || startupOpenSmokeTest || singleInstanceSmokeHost >= 0)
        {
            ConfigureScintillaNativeLibraries();
            ApplicationConfiguration.Initialize();

            if (singleInstanceSmokeHost >= 0)
                return RunSingleInstanceSmokeHost(args, singleInstanceSmokeHost);

            var startupPaths = args
                .Where(arg => !string.Equals(arg, StartupSmokeTestArgument, StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(arg, StartupOpenSmokeTestArgument, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (startupOpenSmokeTest)
            {
                if (startupPaths.Length == 0) return 2;

                using var workspace = new EditorWorkspaceForm(startupPaths);
                workspace.CreateControl();
                return startupPaths.All(workspace.IsPathOpen) ? 0 : 3;
            }

            using (var workspace = new EditorWorkspaceForm())
            {
                workspace.CreateControl();
                return 0;
            }
        }

        // Normal launches are single-instance per Windows user/session. A second
        // Explorer/Open-with launch forwards its file arguments to the already
        // running workspace and exits instead of creating another editor window.
        using var singleInstance = new SingleInstanceCoordinator();
        if (!singleInstance.IsPrimary)
        {
            return singleInstance.SendPathsToPrimary(args, TimeSpan.FromSeconds(5)) ? 0 : 5;
        }

        ConfigureScintillaNativeLibraries();
        ApplicationConfiguration.Initialize();

        using var mainWorkspace = new EditorWorkspaceForm(args);
        using var fileDropSupport = WorkspaceFileDropSupport.Attach(mainWorkspace);
        mainWorkspace.Shown += (_, _) =>
            singleInstance.StartServer(paths => DispatchExternalPaths(mainWorkspace, paths));
        Application.Run(mainWorkspace);
        return 0;
    }

    private static int RunSingleInstanceSmokeHost(string[] args, int hostArgumentIndex)
    {
        if (args.Length <= hostArgumentIndex + 2) return 10;
        var readyPath = args[hostArgumentIndex + 1];
        var resultPath = args[hostArgumentIndex + 2];
        var success = false;

        using var singleInstance = new SingleInstanceCoordinator();
        if (!singleInstance.IsPrimary) return 11;

        using var workspace = new EditorWorkspaceForm();
        using var timeoutTimer = new System.Windows.Forms.Timer { Interval = 15000 };
        timeoutTimer.Tick += (_, _) =>
        {
            timeoutTimer.Stop();
            if (!workspace.IsDisposed) workspace.Close();
        };

        workspace.Shown += (_, _) =>
        {
            singleInstance.StartServer(paths =>
            {
                DispatchExternalPaths(workspace, paths, () =>
                {
                    success = paths.Count > 0 && paths.All(workspace.IsPathOpen);
                    if (success)
                    {
                        try { File.WriteAllText(resultPath, "ok", Encoding.UTF8); }
                        catch { success = false; }
                    }
                    if (!workspace.IsDisposed) workspace.Close();
                });
            });

            try { File.WriteAllText(readyPath, "ready", Encoding.UTF8); }
            catch
            {
                workspace.Close();
                return;
            }
            timeoutTimer.Start();
        };

        Application.Run(workspace);
        return success ? 0 : 12;
    }

    private static void DispatchExternalPaths(
        EditorWorkspaceForm workspace,
        IReadOnlyList<string> paths,
        Action? afterOpen = null)
    {
        if (workspace.IsDisposed || workspace.Disposing) return;

        void Apply()
        {
            if (workspace.IsDisposed || workspace.Disposing) return;

            foreach (var path in paths)
                workspace.OpenPath(path, select: true);

            if (workspace.WindowState == FormWindowState.Minimized)
                workspace.WindowState = FormWindowState.Normal;
            workspace.Show();
            workspace.BringToFront();
            workspace.Activate();
            afterOpen?.Invoke();
        }

        try
        {
            if (workspace.InvokeRequired) workspace.BeginInvoke((Action)Apply);
            else Apply();
        }
        catch (InvalidOperationException)
        {
            // The workspace can be closing while the pipe receives a late request.
        }
    }

    private static void ConfigureScintillaNativeLibraries()
    {
        foreach (var directory in EnumerateScintillaNativeLibraryDirectories())
        {
            if (!ContainsScintillaNativeLibraries(directory)) continue;
            ScintillaNativeLibrary.SatelliteDirectory = directory;
            return;
        }
    }

    private static IEnumerable<string> EnumerateScintillaNativeLibraryDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rid = RuntimeInformation.ProcessArchitecture switch
        {
            RuntimeArchitecture.X64 => "win-x64",
            RuntimeArchitecture.X86 => "win-x86",
            RuntimeArchitecture.Arm64 => "win-arm64",
            RuntimeArchitecture.Arm => "win-arm",
            _ => "win-x64"
        };

        IEnumerable<string> Expand(string? root)
        {
            if (string.IsNullOrWhiteSpace(root)) yield break;

            string full;
            try { full = Path.GetFullPath(root.Trim()); }
            catch { yield break; }

            if (seen.Add(full)) yield return full;

            var runtimeNative = Path.Combine(full, "runtimes", rid, "native");
            if (seen.Add(runtimeNative)) yield return runtimeNative;
        }

        // The single-file host adds the extraction destination(s) to this runtime
        // property. This is the primary source for self-extracted native assets.
        if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is string nativeSearchDirectories)
        {
            foreach (var directory in nativeSearchDirectories.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                foreach (var candidate in Expand(directory)) yield return candidate;
            }
        }

        // Normal folder-based publish / development fallback.
        foreach (var candidate in Expand(AppContext.BaseDirectory)) yield return candidate;
        foreach (var candidate in Expand(Path.GetDirectoryName(Environment.ProcessPath))) yield return candidate;

        // Defensive single-file fallback. The .NET host extracts bundles below
        // %TEMP%\.net\<process-name>\<bundle-id>. The runtime search property above
        // should normally be sufficient, but this keeps renamed EXEs and host changes
        // diagnosable without requiring companion files next to the executable.
        var processName = Path.GetFileNameWithoutExtension(Environment.ProcessPath);
        if (string.IsNullOrWhiteSpace(processName)) yield break;

        var bundleRoot = Path.Combine(Path.GetTempPath(), ".net", processName);
        if (!Directory.Exists(bundleRoot)) yield break;

        IEnumerable<string> bundleDirectories;
        try
        {
            bundleDirectories = Directory.EnumerateDirectories(bundleRoot)
                .OrderByDescending(path => Directory.GetLastWriteTimeUtc(path))
                .ToArray();
        }
        catch
        {
            yield break;
        }

        foreach (var bundleDirectory in bundleDirectories)
        {
            foreach (var candidate in Expand(bundleDirectory)) yield return candidate;
        }
    }

    private static bool ContainsScintillaNativeLibraries(string directory)
    {
        try
        {
            return File.Exists(Path.Combine(directory, "Scintilla.dll")) &&
                   File.Exists(Path.Combine(directory, "Lexilla.dll"));
        }
        catch
        {
            return false;
        }
    }
}
