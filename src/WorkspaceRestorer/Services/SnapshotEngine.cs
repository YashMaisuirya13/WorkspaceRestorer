using WorkspaceRestorer.Models;

namespace WorkspaceRestorer.Services;

internal class SnapshotEngine : IDisposable
{
    private readonly Timer _timer;
    private readonly object _tickLock = new();
    private SessionSnapshot? _lastSnapshot;
    private AppSettings _settings;
    private bool _disposed;

    public event Action<SessionSnapshot>? SnapshotTaken;

    public SnapshotEngine(AppSettings settings)
    {
        _settings = settings;
        var interval = Math.Clamp(settings.SnapshotIntervalSeconds, Constants.MinSnapshotIntervalSeconds, Constants.MaxSnapshotIntervalSeconds);
        _timer = new Timer(OnTick, null, TimeSpan.FromSeconds(interval), TimeSpan.FromSeconds(interval));
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        var interval = Math.Clamp(settings.SnapshotIntervalSeconds, Constants.MinSnapshotIntervalSeconds, Constants.MaxSnapshotIntervalSeconds);
        _timer.Change(TimeSpan.FromSeconds(interval), TimeSpan.FromSeconds(interval));
    }

    public void CaptureNow()
    {
        OnTick(null);
    }

    private void OnTick(object? state)
    {
        if (_disposed) return;

        lock (_tickLock)
        {
            try
            {
                var windows = WindowEnumerator.Capture(_settings.SkipMinimizedWindows);

                // Avoid replacing a meaningful session with an empty list immediately upon startup
                if (windows.Count == 0 && _lastSnapshot is null)
                    return;

                var snapshot = new SessionSnapshot
                {
                    Timestamp = DateTime.Now,
                    Windows = windows
                };

                if (SnapshotsEqual(_lastSnapshot, snapshot))
                    return;

                _lastSnapshot = snapshot;
                SessionStore.Save(snapshot);
                SnapshotTaken?.Invoke(snapshot);
            }
            catch (Exception ex)
            {
                Logger.Log($"Snapshot error: {ex.Message}");
            }
            finally
            {
                // Release unneeded memory back to Windows OS while idle
                TrimWorkingSet();
            }
        }
    }

    [System.Runtime.InteropServices.DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    private static void TrimWorkingSet()
    {
        try
        {
            using var current = System.Diagnostics.Process.GetCurrentProcess();
            EmptyWorkingSet(current.Handle);
        }
        catch { }
    }

    private static bool SnapshotsEqual(SessionSnapshot? a, SessionSnapshot? b)
    {
        if (a is null || b is null) return false;
        if (a.Windows.Count != b.Windows.Count) return false;

        var setA = a.Windows
            .Select(w => $"{w.ExePath}|{w.Title}|{w.Left},{w.Top},{w.Right},{w.Bottom}|{w.ShowCmd}")
            .OrderBy(x => x, StringComparer.Ordinal);
        var setB = b.Windows
            .Select(w => $"{w.ExePath}|{w.Title}|{w.Left},{w.Top},{w.Right},{w.Bottom}|{w.ShowCmd}")
            .OrderBy(x => x, StringComparer.Ordinal);

        return setA.SequenceEqual(setB, StringComparer.Ordinal);
    }

    public void Dispose()
    {
        _disposed = true;
        _timer?.Dispose();
    }
}
