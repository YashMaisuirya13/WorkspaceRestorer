using System.Management;

namespace WorkspaceRestorer.Services;

internal static class CommandLineGrabber
{
    private static readonly Dictionary<int, string> Cache = new();
    private static readonly object CacheLock = new();

    public static string GetCommandLine(int pid, string exePath)
    {
        lock (CacheLock)
        {
            if (Cache.TryGetValue(pid, out var cached))
                return cached;
        }

        var result = string.Empty;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            foreach (ManagementObject obj in searcher.Get())
            {
                result = obj["CommandLine"]?.ToString() ?? string.Empty;
                break;
            }
        }
        catch
        {
            // WMI unavailable or access denied
        }

        if (string.IsNullOrWhiteSpace(result))
            result = $"\"{exePath}\"";

        lock (CacheLock)
        {
            Cache[pid] = result;
            if (Cache.Count > 500)
            {
                var oldest = Cache.Keys.Take(100).ToList();
                foreach (var key in oldest)
                    Cache.Remove(key);
            }
        }

        return result;
    }

    public static void ClearCache(int pid)
    {
        lock (CacheLock)
        {
            Cache.Remove(pid);
        }
    }
}
