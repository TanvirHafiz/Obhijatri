using System.Diagnostics;
using Obhijatri.Core;

namespace Obhijatri.App.Services;

/// <summary>
/// Where the browser's data (history, settings, engine profile, logs) really is on disk. A packaged
/// (MSIX) app writes to %LOCALAPPDATA%\Obhijatri as far as its own code can tell, but Windows redirects
/// that into the package's private folder (and removes it at uninstall), so File Explorer must be
/// pointed at the redirected place to show anything.
/// </summary>
internal static class DataFolder
{
    /// <summary>The folder to show the person, and to open in File Explorer.</summary>
    public static string Path { get; } = ResolvePath();

    /// <summary>True when the app runs from an MSIX package.</summary>
    public static bool IsPackaged { get; } = DetectPackaged();

    public static void Open()
    {
        try
        {
            Directory.CreateDirectory(Path);
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { Path } });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            // Nothing sensible to show if Explorer cannot start.
        }
    }

    private static bool DetectPackaged()
    {
        try
        {
            _ = Windows.ApplicationModel.Package.Current.Id;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false; // unpackaged: no package identity
        }
    }

    private static string ResolvePath()
    {
        if (!DetectPackaged())
        {
            return AppPaths.DataRoot;
        }

        try
        {
            return System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalCacheFolder.Path, "Local", "Obhijatri");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return AppPaths.DataRoot;
        }
    }
}
