using Microsoft.Win32;

namespace WorkspaceRestorer.Services;

internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "WorkspaceRestorer";

    private static string StartupFolder =>
        Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    private static string LegacyShortcutPath =>
        Path.Combine(StartupFolder, "WorkspaceRestorer.lnk");

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            if (key?.GetValue(RunValueName) is not null)
                return true;
        }
        catch { }

        return File.Exists(LegacyShortcutPath);
    }

    public static void Enable(string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.SetValue(RunValueName, $"\"{exePath}\"");
        }
        catch (Exception ex)
        {
            Logger.Log($"Registry startup enable failed: {ex.Message}");
        }

        // Clean up legacy shortcut if present to avoid dual-launching
        try
        {
            if (File.Exists(LegacyShortcutPath))
                File.Delete(LegacyShortcutPath);
        }
        catch { }
    }

    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.DeleteValue(RunValueName, false);
        }
        catch { }

        try
        {
            if (File.Exists(LegacyShortcutPath))
                File.Delete(LegacyShortcutPath);
        }
        catch { }
    }

    public static void SetEnabled(bool enabled, string exePath)
    {
        if (enabled)
            Enable(exePath);
        else
            Disable();
    }
}
