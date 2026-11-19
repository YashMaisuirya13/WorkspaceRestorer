using System.Text.Json;
using WorkspaceRestorer.Models;

namespace WorkspaceRestorer.Services;

internal static class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly object FileLock = new();

    public static SessionSnapshot? Load()
    {
        lock (FileLock)
        {
            try
            {
                if (!File.Exists(Constants.SessionFilePath))
                    return null;

                var json = File.ReadAllText(Constants.SessionFilePath);
                return JsonSerializer.Deserialize<SessionSnapshot>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }

    public static void Save(SessionSnapshot snapshot)
    {
        lock (FileLock)
        {
            Directory.CreateDirectory(Constants.DataDirectory);
            var tmpPath = Constants.SessionFilePath + ".tmp";
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            File.WriteAllText(tmpPath, json);

            if (File.Exists(Constants.SessionFilePath))
                File.Replace(tmpPath, Constants.SessionFilePath, null);
            else
                File.Move(tmpPath, Constants.SessionFilePath);
        }
    }

    public static void Delete()
    {
        lock (FileLock)
        {
            try
            {
                if (File.Exists(Constants.SessionFilePath))
                    File.Delete(Constants.SessionFilePath);
            }
            catch { }
        }
    }

    public static SessionSnapshot? LoadCrashSession()
    {
        lock (FileLock)
        {
            try
            {
                if (!File.Exists(Constants.CrashBackupFilePath))
                    return null;

                var json = File.ReadAllText(Constants.CrashBackupFilePath);
                return JsonSerializer.Deserialize<SessionSnapshot>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }

    public static void SaveCrashSession(SessionSnapshot snapshot)
    {
        lock (FileLock)
        {
            try
            {
                Directory.CreateDirectory(Constants.DataDirectory);
                var tmpPath = Constants.CrashBackupFilePath + ".tmp";
                var json = JsonSerializer.Serialize(snapshot, JsonOptions);
                File.WriteAllText(tmpPath, json);

                if (File.Exists(Constants.CrashBackupFilePath))
                    File.Replace(tmpPath, Constants.CrashBackupFilePath, null);
                else
                    File.Move(tmpPath, Constants.CrashBackupFilePath);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to save crash backup session: {ex.Message}");
            }
        }
    }

    public static void ClearCrashSession()
    {
        lock (FileLock)
        {
            try
            {
                if (File.Exists(Constants.CrashBackupFilePath))
                    File.Delete(Constants.CrashBackupFilePath);
            }
            catch { }
        }
    }

    public static bool HasCrashSession()
    {
        lock (FileLock)
        {
            return File.Exists(Constants.CrashBackupFilePath);
        }
    }
}
