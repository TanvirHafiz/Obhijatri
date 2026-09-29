namespace Obhijatri.Safety.Downloads;

/// <summary>
/// Flags file names that try to hide an executable behind an earlier, harmless-looking extension,
/// for example "invoice.pdf.exe" or "photo.jpg.scr". Windows Explorer hides known extensions by
/// default, so such a name can display as just "invoice.pdf" with the PDF icon.
/// </summary>
public static class DangerousExtensions
{
    private static readonly HashSet<string> Executable = new(StringComparer.OrdinalIgnoreCase)
    {
        "exe", "scr", "com", "pif", "bat", "cmd", "msi", "msp", "msc", "vbs", "vbe", "js", "jse",
        "wsf", "wsh", "ps1", "psm1", "hta", "cpl", "jar", "gadget", "reg", "lnk", "vb", "vbscript",
    };

    /// <summary>True if the final extension is one Windows can execute or script on its own.</summary>
    public static bool IsExecutable(string fileName) =>
        Executable.Contains(Path.GetExtension(fileName).TrimStart('.'));

    /// <summary>
    /// True for a name with two or more extensions where the last one is executable and an earlier
    /// one looks like an ordinary document, image, audio, video or archive type: the double-extension
    /// trick. A name that is executable all the way through (installer.msi.exe) is still caught by
    /// <see cref="IsExecutable"/> alone and does not need this check.
    /// </summary>
    public static bool IsDoubleExtensionTrick(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var parts = name.Split('.');
        if (parts.Length < 3)
        {
            return false;
        }

        var last = parts[^1];
        if (!Executable.Contains(last))
        {
            return false;
        }

        // Any earlier segment that reads as a plausible, non-executable extension is enough:
        // the point is that the visible (or first-glance) extension is not the real one.
        for (var i = 1; i < parts.Length - 1; i++)
        {
            var candidate = parts[i];
            if (candidate.Length is > 0 and <= 5 && candidate.All(char.IsLetterOrDigit) && !Executable.Contains(candidate))
            {
                return true;
            }
        }
        return false;
    }
}
