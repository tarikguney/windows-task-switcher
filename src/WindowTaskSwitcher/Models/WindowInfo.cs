using System.Windows.Media.Imaging;

namespace WindowTaskSwitcher.Models;

public sealed class WindowInfo
{
    public required IntPtr Handle { get; init; }
    public required string Title { get; init; }
    public required string ProcessName { get; init; }
    public required uint ProcessId { get; init; }
    public BitmapSource? Icon { get; set; }
    public Guid VirtualDesktopId { get; set; }
    public bool IsOnCurrentDesktop { get; set; } = true;

    /// <summary>
    /// Combined string used for fuzzy search matching.
    /// </summary>
    public string SearchText => $"{Title} {ProcessName}";
}
