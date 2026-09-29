using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Obhijatri.App.Browser;
using Obhijatri.App.Localization;
using Obhijatri.Safety.Downloads;

namespace Obhijatri.App.Downloads;

/// <summary>
/// One download shown in the downloads panel. A finished download is scanned (Mark of the Web plus
/// whatever antivirus is registered for downloads, and a check for a hidden second extension like
/// "invoice.pdf.exe") before it can be opened; see Obhijatri.Safety/Downloads/AttachmentScanner.cs.
/// </summary>
public sealed partial class DownloadItem : ObservableBase
{
    private readonly CoreWebView2DownloadOperation _operation;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
    private string _statusText = string.Empty;
    private double _progress;
    private bool _isIndeterminate;
    private bool _isInProgress = true;
    private bool _isScanning;
    private bool _isCompleted;
    private bool _isBlocked;
    private bool _scanStarted;

    internal DownloadItem(CoreWebView2DownloadOperation operation)
    {
        _operation = operation;
        FilePath = operation.ResultFilePath;
        FileName = Path.GetFileName(FilePath);
        operation.BytesReceivedChanged += (_, _) => Refresh();
        operation.StateChanged += (_, _) => Refresh();
        Refresh();
    }

    public string FileName { get; }
    public string FilePath { get; }

    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public double Progress { get => _progress; private set => Set(ref _progress, value); }
    public bool IsIndeterminate { get => _isIndeterminate; private set => Set(ref _isIndeterminate, value); }
    public bool IsInProgress { get => _isInProgress; private set => Set(ref _isInProgress, value); }
    public bool IsScanning { get => _isScanning; private set => Set(ref _isScanning, value); }
    public bool IsCompleted { get => _isCompleted; private set => Set(ref _isCompleted, value); }

    /// <summary>Scanning found a problem: the file cannot be opened from here, only deleted.</summary>
    public bool IsBlocked
    {
        get => _isBlocked;
        private set
        {
            if (Set(ref _isBlocked, value))
            {
                Raise(nameof(IsNotBlocked));
                Raise(nameof(StatusStyle));
            }
        }
    }

    public bool IsNotBlocked => !IsBlocked;

    /// <summary>A theme-aware style: the blocked message reads as an error, everything else as normal.</summary>
    public Microsoft.UI.Xaml.Style StatusStyle => (Microsoft.UI.Xaml.Style)Microsoft.UI.Xaml.Application.Current.Resources[
        IsBlocked ? "CriticalCaptionTextBlockStyle" : "CaptionTextBlockStyle"];

    public void Cancel()
    {
        if (IsInProgress)
        {
            _operation.Cancel();
        }
    }

    public void Open()
    {
        if (IsCompleted && !IsBlocked && File.Exists(FilePath))
        {
            Process.Start(new ProcessStartInfo(FilePath) { UseShellExecute = true });
        }
    }

    /// <summary>Removes a blocked file (there is nothing useful to do with it from here but delete it).</summary>
    public void DeleteBlocked()
    {
        if (!IsBlocked)
        {
            return;
        }
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch (IOException)
        {
            // Already gone, or in use; either way there is nothing more to do here.
        }
        StatusText = Strings.Get("DownloadDeleted");
    }

    /// <summary>
    /// Applies Mark of the Web and runs the registered antivirus scan, then checks for a hidden
    /// second extension (invoice.pdf.exe). Runs once per completed download, off the UI thread.
    /// </summary>
    private void StartScan()
    {
        if (_scanStarted)
        {
            return;
        }
        _scanStarted = true;
        IsScanning = true;
        StatusText = Strings.Get("DownloadScanning");
        var path = FilePath;
        var source = _operation.Uri;
        var name = FileName;
        _ = Task.Run(() =>
        {
            var verdict = AttachmentScanner.Scan(path, source, referrerUrl: null);
            var blocked = verdict == ScanVerdict.Blocked || DangerousExtensions.IsDoubleExtensionTrick(name);
            _dispatcher.TryEnqueue(() => FinishScan(blocked));
        });
    }

    private void FinishScan(bool blocked)
    {
        IsScanning = false;
        if (blocked)
        {
            IsBlocked = true;
            StatusText = Strings.Get("DownloadBlocked");
        }
        else
        {
            IsCompleted = true;
            StatusText = Strings.Format("DownloadCompletedFormat", Formatting.Bytes(_operation.BytesReceived));
        }
    }

    public void ShowInFolder()
    {
        if (File.Exists(FilePath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { "/select,", FilePath } });
        }
        else if (Path.GetDirectoryName(FilePath) is { } folder && Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { folder } });
        }
    }

    private void Refresh()
    {
        var received = _operation.BytesReceived;
        var total = _operation.TotalBytesToReceive is long t and > 0 ? t : 0;

        switch (_operation.State)
        {
            case CoreWebView2DownloadState.InProgress:
                IsInProgress = true;
                IsIndeterminate = total == 0;
                Progress = total == 0 ? 0 : 100.0 * received / total;
                StatusText = total == 0
                    ? Formatting.Bytes(received)
                    : Strings.Format("DownloadProgressFormat", Formatting.Bytes(received), Formatting.Bytes(total));
                break;

            case CoreWebView2DownloadState.Completed:
                IsInProgress = false;
                Progress = 100;
                StartScan();
                break;

            default:
                IsInProgress = false;
                StatusText = _operation.InterruptReason == CoreWebView2DownloadInterruptReason.UserCanceled
                    ? Strings.Get("DownloadCanceled")
                    : Strings.Get("DownloadFailed");
                break;
        }
    }
}
