namespace WorkspaceRestorer.Services;

internal static class Logger
{
    private static readonly object Lock = new();

    public static void Log(string message)
    {
        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(Constants.DataDirectory);
                File.AppendAllText(Constants.LogFilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
