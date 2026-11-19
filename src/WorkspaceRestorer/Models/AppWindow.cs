namespace WorkspaceRestorer.Models;

public class AppWindow
{
    public uint Pid { get; set; }
    public string ExePath { get; set; } = string.Empty;
    public string CommandLine { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;

    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
    public int ShowCmd { get; set; }
    public int MinimizedX { get; set; }
    public int MinimizedY { get; set; }
    public int MaximizedX { get; set; }
    public int MaximizedY { get; set; }
    public int PlacementFlags { get; set; }

    public bool IsMinimized => ShowCmd == 2 || ShowCmd == 7;
    public bool IsMaximized => ShowCmd == 3;
}
