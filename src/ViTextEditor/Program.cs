using System.Runtime.InteropServices;
using System.Text;
using ScintillaNET;
using RuntimeArchitecture = System.Runtime.InteropServices.Architecture;

namespace ViTextEditor;

internal static class Program
{
    private const string StartupSmokeTestArgument = "--startup-smoke-test";
    private const string StartupOpenSmokeTestArgument = "--startup-open-smoke-test";
    private const string UrlIndicatorSmokeTestArgument = "--url-indicator-smoke-test";
    private const string SingleInstanceReceiveSmokeTestArgument = "--single-instance-receive-smoke-test";

    [STAThread]
    private static int Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Scintilla5.NET 7.x looks for Scintilla.dll / Lexilla.dll as real files.
        // In a .NET single-file build those native files are self-extracted under
        // %TEMP%\.net\... before managed Main starts. Tell Scintilla where the
        // runtime actually placed them before the Scintilla type is first used.
        ConfigureScintillaNativeLibraries();

        ApplicationConfiguration.Initialize();

        var startupSmokeTest = args.Any(arg => string.Equals(arg, StartupSmokeTestArgument, StringComparison.OrdinalIgnoreCase));
        var startupOpenSmokeTest = args.Any(arg => string.Equals(arg, StartupOpenSmokeTestArgument, StringComparison.OrdinalIgnoreCase));
        var urlIndicatorSmokeTest = args.Any(arg => string.Equals(arg, UrlIndicatorSmokeTestArgument, StringComparison.OrdinalIgnoreCase));
        var singleInstanceSmokeIndex = Array.FindIndex(args, arg =>
            string.Equals(arg, SingleInstanceReceiveSmokeTestArgument, StringComparison.OrdinalIgnoreCase));
        var singleInstanceSmokeTest = singleInstanceSmokeIndex >= 0;
        var singleInstanceMarker = singleInstanceSmokeTest && args.Length > singleInstanceSmokeIndex + 1
            ? args[singleInstanceSmokeIndex + 1]
            : null;
        var singleInstanceExpectedPath = singleInstanceSmokeTest && args.Length > singleInstanceSmokeIndex + 2
            ? args[singleInstanceSmokeIndex + 2]
            : null;
        var startupPaths = args
            .Where(arg => !string.Equals(arg, StartupSmokeTestArgument, StringComparison.OrdinalIgnoreCase) &&
                          !string.Equals(arg, StartupOpenSmokeTestArgument, StringComparison.OrdinalIgnoreCase) &&
                          !string.Equals(arg, UrlIndicatorSmokeTestArgument, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (singleInstanceSmokeTest)
        {
            if (singleInstanceMarker is null || singleInstanceExpectedPath is null) return 5;
            startupPaths = [];
        }

        if (startupOpenSmokeTest)
        {
            if (startupPaths.Length == 0) return 2;

            using var smokeWorkspace = new EditorWorkspaceForm(startupPaths);
            smokeWorkspace.CreateControl();
            return startupPaths.All(smokeWorkspace.IsPathOpen) ? 0 : 3;
        }

        if (startupSmokeTest)
        {
            // CI/local packaging smoke test: constructing the workspace creates the
            // embedded MainForm and Scintilla control, so native-library startup
            // regressions fail here instead of producing a silently exiting EXE.
            using var smokeWorkspace = new EditorWorkspaceForm();
            smokeWorkspace.CreateControl();
            return 0;
        }

        if (urlIndicatorSmokeTest)
        {
            using var smokeForm = new MainForm();
            smokeForm.CreateControl();
            return smokeForm.RunUrlIndicatorSmokeTest() ? 0 : 6;
        }

        using var broker = SingleInstanceFileBroker.Acquire();
        if (!broker.IsPrimary)
        {
            return SingleInstanceFileBroker.ForwardFiles(startupPaths, TimeSpan.FromSeconds(5)) ? 0 : 4;
        }

        // Windows Explorer / "Open with" passes selected files as command-line
        // arguments. A later launch forwards those files to this primary workspace.
        using var workspace = new EditorWorkspaceForm(startupPaths);
        workspace.CreateControl();
        broker.StartListening(paths =>
        {
            if (workspace.IsDisposed || workspace.Disposing) return;
            try
            {
                workspace.BeginInvoke(new Action(() =>
                {
                    if (workspace.IsDisposed || workspace.Disposing) return;
                    workspace.OpenPaths(paths);
                    workspace.ActivateFromExternalRequest();

                    if (singleInstanceSmokeTest &&
                        singleInstanceMarker is not null &&
                        singleInstanceExpectedPath is not null &&
                        workspace.IsPathOpen(singleInstanceExpectedPath))
                    {
                        File.WriteAllText(singleInstanceMarker, "PASS");
                        workspace.Close();
                    }
                }));
            }
            catch (InvalidOperationException) when (workspace.IsDisposed || workspace.Disposing) { }
        });

        Application.Run(workspace);
        return 0;
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
