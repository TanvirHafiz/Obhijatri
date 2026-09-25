using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Obhijatri.App.Browser;
using Obhijatri.App.Localization;

namespace Obhijatri.App.Downloads;

/// <summary>One download shown in the downloads panel. Scanning arrives in Milestone 7.</summary>
public sealed partial class DownloadItem : ObservableBase
{
    private readonly CoreWebView2DownloadOperation _operation;
    private string _statusText = string.Empty;
    private double _progress;
    private bool _isIndeterminate;
    private bool _isInProgress = true;
    private bool _isCompleted;

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
    public bool IsCompleted { get => _isCompleted; private set => Set(ref _isCompleted, value); }

    public void Cancel()
    {
        if (IsInProgress)
        {
            _operation.Cancel();
        }
    }

    public void Open()
    {
        if (IsCompleted && File.Exists(FilePath))
        {
            Process.Start(new ProcessStartInfo(FilePath) { UseShellExecute = true });
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
                IsCompleted = true;
                Progress = 100;
                StatusText = Strings.Format("DownloadCompletedFormat", Formatting.Bytes(received));
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
