using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Obhijatri.App;

/// <summary>
/// Entry point. Only one Obhijatri process runs at a time: a second launch hands over to the
/// running one (which comes to the front and opens a new tab) and exits. Two processes would
/// otherwise write the same database and saved session.
/// </summary>
public static class Program
{
    private const string InstanceKey = "Obhijatri.Main";

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!mainInstance.IsCurrent)
        {
            RedirectTo(mainInstance);
            return 0;
        }

        mainInstance.Activated += (_, _) => App.OnActivatedFromAnotherLaunch();

        Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }

    private static void RedirectTo(AppInstance mainInstance)
    {
        // Let the running instance bring its window to the front.
        AllowSetForegroundWindow((int)mainInstance.ProcessId);

        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        using var done = new ManualResetEvent(false);
        Task.Run(() =>
        {
            try
            {
                mainInstance.RedirectActivationToAsync(activation).AsTask().Wait(TimeSpan.FromSeconds(10));
            }
            finally
            {
                done.Set();
            }
        });
        // WaitOne on an STA thread keeps pumping COM messages while we wait.
        done.WaitOne();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
