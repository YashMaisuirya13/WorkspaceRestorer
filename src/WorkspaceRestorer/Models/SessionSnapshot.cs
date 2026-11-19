namespace WorkspaceRestorer.Models;

public class SessionSnapshot
{
    public DateTime Timestamp { get; set; }
    public List<AppWindow> Windows { get; set; } = new();
    public bool IsCrashRecovery { get; set; }
}
