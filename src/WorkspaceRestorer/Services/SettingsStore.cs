using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkspaceRestorer.Services;

public class AppSettings
{
    public int SnapshotIntervalSeconds { get; set; } = Constants.DefaultSnapshotIntervalSeconds;
    public bool StartWithWindows { get; set; } = true;
    public bool SkipMinimizedWindows { get; set; }
    public bool RestoreMaximizedAsMaximized { get; set; } = true;
    public int LaunchDelayMs { get; set; } = Constants.LaunchDelayMs;
}

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(Constants.SettingsFilePath))
            {
                var json = File.ReadAllText(Constants.SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Constants.DataDirectory);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(Constants.SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to save settings: {ex.Message}");
        }
    }
}
