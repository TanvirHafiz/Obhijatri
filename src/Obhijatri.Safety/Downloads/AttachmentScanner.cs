using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace Obhijatri.Safety.Downloads;

public enum ScanVerdict
{
    /// <summary>No antivirus product objected, and the file is still where it was saved.</summary>
    Clean,

    /// <summary>Blocked or removed: an antivirus product reported an infection, or policy blocked it.</summary>
    Blocked,

    /// <summary>The scan itself could not run (older Windows, no provider registered, COM failure). Not a verdict either way.</summary>
    Unknown,
}

/// <summary>
/// Runs a finished download through Windows' Attachment Execution Services
/// (<c>IAttachmentExecute</c>), the same mechanism email clients and other browsers use: it applies
/// Mark of the Web (so Windows treats the file as coming from the internet) and, on
/// <see cref="IAttachmentExecute.Save"/>, invokes every antivirus product registered for downloaded
/// files (Microsoft Defender among them) before the call returns. Nothing about the file is sent
/// anywhere by this class; the antivirus product itself may have its own cloud lookups, the same as
/// it does for any other file on the PC.
///
/// The HRESULT handling mirrors the approach documented for Chromium's downloader: a security-policy
/// or antivirus HRESULT is treated as blocked; the file having disappeared after a failing call is
/// also treated as blocked (some products delete rather than return a distinct code); any other
/// failure is reported as <see cref="ScanVerdict.Unknown"/> rather than guessed at.
/// </summary>
[SupportedOSPlatform("windows")]
public static class AttachmentScanner
{
    // Fixed per plan.md's app identity: generated once for Obhijatri, not a per-install secret.
    private static readonly Guid ClientId = new("6a4b9d0a-6e6c-4b7a-8b7b-6f7b6a2f5b8e");

    private const int INET_E_SECURITY_PROBLEM = unchecked((int)0x800C000E);
    private const int E_FAIL = unchecked((int)0x80004005);

    /// <summary>
    /// Synchronous and, in practice, slow (it waits for every registered antivirus product): call
    /// this from a background thread, never the UI thread.
    /// </summary>
    public static ScanVerdict Scan(string localPath, string? sourceUrl, string? referrerUrl)
    {
        // This COM object only answers correctly from a single-threaded apartment: on a plain
        // thread-pool (MTA) thread, QueryInterface for IAttachmentExecute fails with E_NOINTERFACE
        // even though the same call succeeds on an STA thread. Confirmed against the real Windows
        // component this session (see PROGRESS.md), not assumed from documentation.
        ScanVerdict result = ScanVerdict.Unknown;
        var thread = new Thread(() => result = ScanOnStaThread(localPath, sourceUrl, referrerUrl));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    private static ScanVerdict ScanOnStaThread(string localPath, string? sourceUrl, string? referrerUrl)
    {
        IAttachmentExecute? execute = null;
        try
        {
            execute = (IAttachmentExecute)new AttachmentServices();
            var clientId = ClientId;
            execute.SetClientGuid(ref clientId);
            execute.SetLocalPath(localPath);
            execute.SetSource(NormalizeUrl(sourceUrl));
            if (!string.IsNullOrEmpty(referrerUrl) && referrerUrl.Length <= 2048)
            {
                execute.SetReferrer(referrerUrl);
            }

            var hr = execute.Save();
            if (hr >= 0)
            {
                return File.Exists(localPath) ? ScanVerdict.Clean : ScanVerdict.Blocked;
            }
            if (hr == INET_E_SECURITY_PROBLEM || hr == E_FAIL)
            {
                return ScanVerdict.Blocked;
            }
            return File.Exists(localPath) ? ScanVerdict.Unknown : ScanVerdict.Blocked;
        }
        catch (COMException)
        {
            return ScanVerdict.Unknown;
        }
        catch (InvalidCastException)
        {
            return ScanVerdict.Unknown;
        }
        finally
        {
            if (execute is not null)
            {
                Marshal.ReleaseComObject(execute);
            }
        }
    }

    /// <summary>
    /// Windows Attachment Execution Services caps the source string and treats an empty one as the
    /// unrestricted local zone; "about:internet" is the documented placeholder for "from the internet,
    /// exact address unknown", used the same way by other browsers.
    /// </summary>
    private static string NormalizeUrl(string? url) =>
        string.IsNullOrEmpty(url) || url.Length > 2048 ? "about:internet" : url;

    [ComImport, Guid("4125DD96-E03A-4103-8F70-E0597D803B9C")]
    private class AttachmentServices;

    [ComImport, Guid("73DB1241-1E85-4581-8E4F-A81E1D0F8C57"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAttachmentExecute
    {
        void SetClientTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetClientGuid(ref Guid guid);
        void SetLocalPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void SetSource([MarshalAs(UnmanagedType.LPWStr)] string source);
        void SetReferrer([MarshalAs(UnmanagedType.LPWStr)] string referrer);
        void CheckPolicy();
        void Prompt(IntPtr hwnd, int prompt, out int action);
        [PreserveSig] int Save();
        void Execute(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string? verb, out IntPtr process);
        void SaveWithUI(IntPtr hwnd);
        void ClearClientState();
    }
}
