namespace WorkspaceRestorer.Services;

internal static class CrashDetector
{
    public static void MarkCleanShutdown()
    {
        try
        {
            Directory.CreateDirectory(Constants.DataDirectory);
            File.WriteAllText(Constants.SentinelFilePath, DateTime.Now.ToString("O"));
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to write sentinel: {ex.Message}");
        }
    }

    public static bool WasUncleanShutdown()
    {
        return !File.Exists(Constants.SentinelFilePath);
    }

    public static void ClearSentinel()
    {
        try
        {
            if (File.Exists(Constants.SentinelFilePath))
                File.Delete(Constants.SentinelFilePath);
        }
        catch { }
    }
}
