using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WorkspaceRestorer.Interop;
using WorkspaceRestorer.Models;

namespace WorkspaceRestorer.Services;

internal static class WindowEnumerator
{
    private static readonly HashSet<string> ExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApplicationFrameHost",
        "explorer",
        "SearchHost",
        "ShellExperienceHost",
        "StartMenuExperienceHost",
        "TextInputHost",
        "Widgets",
        "WidgetService",
        "LockApp",
        "consent",
        "dwm",
        "csrss",
        "sihost",
        "taskhostw",
        "RuntimeBroker",
        "SystemSettings",
        "Settings",
        "SecurityHealthSystray",
        "SecurityHealthService",
        "OneDrive",
        "YourPhone",
        "PhoneExperienceHost",
        "wsreset",
        "perfmon",
        "mmc",
        "mmcsh",
        "OfficeClickToRun",
        "SpotifyWebHelper"
    };

    public static List<AppWindow> Capture(bool skipMinimized = false)
    {
        var results = new List<AppWindow>();
        var currentPid = (uint)Environment.ProcessId;
        var shellWindow = NativeMethods.GetShellWindow();
        var desktopWindow = NativeMethods.GetDesktopWindow();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            try
            {
                if (hWnd == shellWindow || hWnd == desktopWindow)
                    return true;

                if (!NativeMethods.IsWindowVisible(hWnd))
                    return true;

                if (IsCloaked(hWnd))
                    return true;

                NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
                if (pid == 0 || pid == currentPid)
                    return true;

                var exePath = GetProcessPath((int)pid);
                if (string.IsNullOrWhiteSpace(exePath))
                    return true;

                var processName = Path.GetFileNameWithoutExtension(exePath);
                if (ExcludedProcessNames.Contains(processName))
                    return true;

                var titleLength = NativeMethods.GetWindowTextLength(hWnd);
                if (titleLength == 0)
                    return true;

                var title = new StringBuilder(titleLength + 1);
                NativeMethods.GetWindowText(hWnd, title, title.Capacity);

                var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
                if (!NativeMethods.GetWindowPlacement(hWnd, ref placement))
                    return true;

                if (skipMinimized && (placement.showCmd == 2 || placement.showCmd == 7))
                    return true;

                var commandLine = CommandLineGrabber.GetCommandLine((int)pid, exePath);

                results.Add(new AppWindow
                {
                    Pid = pid,
                    ExePath = exePath,
                    CommandLine = commandLine,
                    Title = title.ToString(),
                    ProcessName = processName,
                    Left = placement.rcNormalPosition.Left,
                    Top = placement.rcNormalPosition.Top,
                    Right = placement.rcNormalPosition.Right,
                    Bottom = placement.rcNormalPosition.Bottom,
                    ShowCmd = placement.showCmd,
                    MinimizedX = placement.ptMinPosition.X,
                    MinimizedY = placement.ptMinPosition.Y,
                    MaximizedX = placement.ptMaxPosition.X,
                    MaximizedY = placement.ptMaxPosition.Y,
                    PlacementFlags = placement.flags
                });
            }
            catch
            {
                // Access denied or process exited mid-capture — skip
            }

            return true;
        }, IntPtr.Zero);

        // Dedupe by process + title (some apps have multiple HWNDs)
        return results
            .GroupBy(w => $"{w.ExePath}|{w.Title}")
            .Select(g => g.First())
            .ToList();
    }

    private static bool IsCloaked(IntPtr hWnd)
    {
        try
        {
            if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0)
                return cloaked != 0;
        }
        catch { }
        return false;
    }

    private static string GetProcessPath(int pid)
    {
        try
        {
            var hProcess = OpenProcessForPath(pid);
            if (hProcess == IntPtr.Zero)
                return string.Empty;

            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (NativeMethods.QueryFullProcessImageName(hProcess, 0, sb, ref size))
                    return sb.ToString();
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        catch { }
        return string.Empty;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    private static IntPtr OpenProcessForPath(int pid)
    {
        return OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
    }
}
