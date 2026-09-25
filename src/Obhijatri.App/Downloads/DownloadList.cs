using System.Collections.ObjectModel;

namespace Obhijatri.App.Downloads;

/// <summary>Downloads of this app run, newest first. Not saved to disk.</summary>
public sealed partial class DownloadList : ObservableCollection<DownloadItem>
{
    public void AddNewest(DownloadItem item) => Insert(0, item);
}
