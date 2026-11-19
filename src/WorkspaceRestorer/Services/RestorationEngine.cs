using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using WorkspaceRestorer.Interop;
using WorkspaceRestorer.Models;

namespace WorkspaceRestorer.Services;

internal static class RestorationEngine
{
    public record RestoreResult(int Requested, int Succeeded, List<string> Failures);

    public static RestoreResult Restore(IReadOnlyList<AppWindow> windows, AppSettings settings, CancellationToken ct = default)
    {
        int succeeded = 0;
        var failures = new List<string>();

        foreach (var window in windows)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // Baseline BEFORE launch: apps like Win11 Notepad show their
                // window within ~300ms, faster than our launch delay
                var baseline = GetVisibleWindowHandles();

                var psi = BuildStartInfo(window);
                var process = Process.Start(psi);
                if (process is null)
                {
                    failures.Add($"{window.ProcessName}: failed to start");
                    continue;
                }

                if (settings.LaunchDelayMs > 0)
                    Thread.Sleep(settings.LaunchDelayMs);

                var hwnd = WaitForMainWindow(process, window, baseline, Constants.WindowWaitTimeoutMs);
                if (hwnd == IntPtr.Zero)
                {
                    Logger.Log($"Window never appeared for {window.ProcessName} (pid {process.Id})");
                    // Process launched but window not found — count as partial success
                    succeeded++;
                    continue;
                }

                ApplyPlacement(hwnd, window, settings);
                succeeded++;
            }
            catch (Exception ex)
            {
                failures.Add($"{window.ProcessName}: {ex.Message}");
                Logger.Log($"Restore failed for {window.ProcessName}: {ex.Message}");
            }
        }

        return new RestoreResult(windows.Count, succeeded, failures);
    }

    private static ProcessStartInfo BuildStartInfo(AppWindow window)
    {
        if (!string.IsNullOrWhiteSpace(window.CommandLine) &&
            !string.Equals(window.CommandLine.Trim().Trim('"'), window.ExePath, StringComparison.OrdinalIgnoreCase))
        {
            var cmd = window.CommandLine.Trim();
            if (cmd.StartsWith('"') && cmd.Length > 1)
            {
                var closeQuote = cmd.IndexOf('"', 1);
                if (closeQuote > 1)
                {
                    var exe = cmd[..(closeQuote + 1)];
                    var args = cmd[(closeQuote + 1)..].Trim();
                    return new ProcessStartInfo
                    {
                        FileName = exe.Trim('"'),
                        Arguments = args,
                        UseShellExecute = true
                    };
                }
            }
        }

        return new ProcessStartInfo
        {
            FileName = window.ExePath,
            UseShellExecute = true
        };
    }

    private static IntPtr WaitForMainWindow(Process process, AppWindow target, HashSet<IntPtr> baseline, int timeoutMs)
    {
        var targetExeName = Path.GetFileName(target.ExePath);
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            // Fast path: the started process shows its own main window
            try
            {
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                    return process.MainWindowHandle;
            }
            catch { }

            // Robust path: trampoline/child processes (e.g. Win11 Notepad) —
            // find a NEW window whose process exe matches the target exe
            foreach (var hwnd in GetVisibleWindowHandles())
            {
                if (baseline.Contains(hwnd))
                    continue;
                if (WindowMatchesExe(hwnd, targetExeName, process.Id))
                    return hwnd;
            }

            Thread.Sleep(150);
        }

        return IntPtr.Zero;
    }

    private static HashSet<IntPtr> GetVisibleWindowHandles()
    {
        var handles = new HashSet<IntPtr>();
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (NativeMethods.IsWindowVisible(hWnd) && NativeMethods.GetWindowTextLength(hWnd) > 0)
                handles.Add(hWnd);
            return true;
        }, IntPtr.Zero);
        return handles;
    }

    private static bool WindowMatchesExe(IntPtr hwnd, string exeName, int startedPid)
    {
        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return false;
            if (pid == startedPid) return true;

            using var proc = Process.GetProcessById((int)pid);
            return string.Equals(proc.ProcessName + ".exe", exeName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyPlacement(IntPtr hwnd, AppWindow window, AppSettings settings)
    {
        var normalPosition = EnsureVisibleOnDisplay(new RECT
        {
            Left = window.Left,
            Top = window.Top,
            Right = window.Right,
            Bottom = window.Bottom
        });

        var placement = new WINDOWPLACEMENT
        {
            length = Marshal.SizeOf<WINDOWPLACEMENT>(),
            flags = window.PlacementFlags,
            showCmd = window.ShowCmd,
            ptMinPosition = new POINT { X = window.MinimizedX, Y = window.MinimizedY },
            ptMaxPosition = new POINT { X = window.MaximizedX, Y = window.MaximizedY },
            rcNormalPosition = normalPosition
        };

        if (window.IsMaximized && !settings.RestoreMaximizedAsMaximized)
            placement.showCmd = NativeMethods.SW_SHOW;

        if (window.IsMinimized)
            placement.showCmd = NativeMethods.SW_SHOWMINNOACTIVE;

        if (!NativeMethods.SetWindowPlacement(hwnd, ref placement))
        {
            Logger.Log($"SetWindowPlacement failed for {window.ProcessName} (Win32 error {Marshal.GetLastWin32Error()})");
        }
    }

    private static RECT EnsureVisibleOnDisplay(RECT rect)
    {
        // Check if at least partially on an active monitor
        var monitor = NativeMethods.MonitorFromRect(ref rect, NativeMethods.MONITOR_DEFAULTTONULL);
        if (monitor != IntPtr.Zero)
            return rect;

        // Off-screen (e.g. disconnected secondary display): shift to nearest active monitor
        var nearest = NativeMethods.MonitorFromRect(ref rect, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (nearest == IntPtr.Zero)
            return rect;

        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(nearest, ref info))
            return rect;

        var width = Math.Min(rect.Right - rect.Left, info.rcWork.Right - info.rcWork.Left);
        var height = Math.Min(rect.Bottom - rect.Top, info.rcWork.Bottom - info.rcWork.Top);
        if (width <= 0) width = 800;
        if (height <= 0) height = 600;

        var left = Math.Clamp(rect.Left, info.rcWork.Left, info.rcWork.Right - width);
        var top = Math.Clamp(rect.Top, info.rcWork.Top, info.rcWork.Bottom - height);

        return new RECT
        {
            Left = left,
            Top = top,
            Right = left + width,
            Bottom = top + height
        };
    }
}
