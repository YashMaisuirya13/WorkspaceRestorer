namespace WorkspaceRestorer;

internal static class Constants
{
    public const string AppName = "Workspace Session Restorer";
    public const string MutexName = @"Local\WorkspaceRestorer_SingleInstance";

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkspaceRestorer");

    public static string SessionFilePath => Path.Combine(DataDirectory, "session.json");
    public static string CrashBackupFilePath => Path.Combine(DataDirectory, "crash_session.json");
    public static string SentinelFilePath => Path.Combine(DataDirectory, "sentinel.flag");
    public static string SettingsFilePath => Path.Combine(DataDirectory, "settings.json");
    public static string LogFilePath => Path.Combine(DataDirectory, "restorer.log");

    public const int DefaultSnapshotIntervalSeconds = 30;
    public const int MinSnapshotIntervalSeconds = 5;
    public const int MaxSnapshotIntervalSeconds = 300;
    public const int LaunchDelayMs = 300;
    public const int WindowWaitTimeoutMs = 8000;
}
