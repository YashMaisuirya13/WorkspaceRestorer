using System.IO;
using System.Windows;
using WorkspaceRestorer.Models;
using WorkspaceRestorer.Services;
using WorkspaceRestorer.UI;

namespace WorkspaceRestorer;

public partial class App : Application
{
    private static Mutex? _mutex;
    private AppSettings _settings = new();
    private SnapshotEngine? _snapshotEngine;
    private TrayIconService? _tray;
    private RecoveryWindow? _recoveryWindow;
    private SettingsWindow? _settingsWindow;
    private SessionSnapshot? _pendingRecoverySession;
    private bool _cleanExit;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Log($"Unhandled UI error: {args.Exception}");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Logger.Log($"Unhandled domain error: {args.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Log($"Unobserved task error: {args.Exception?.Message}");
            args.SetObserved();
        };

        _mutex = new Mutex(true, Constants.MutexName, out var isNew);
        if (!isNew)
        {
            MessageBox.Show(Constants.AppName + " is already running in the tray.", "Already Running",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Directory.CreateDirectory(Constants.DataDirectory);
        _settings = SettingsStore.Load();

        var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "WorkspaceRestorer.exe");
        if (_settings.StartWithWindows && !StartupManager.IsEnabled())
        {
            try { StartupManager.Enable(exe); } catch { }
        }

        // Crash detection: sentinel exists => clean previous exit; missing => crash
        var wasCrash = CrashDetector.WasUncleanShutdown();
        // Delete sentinel while running — if we crash, it won't be there next time
        CrashDetector.ClearSentinel();

        // Load previous session BEFORE any new capture overwrites it
        var lastSession = SessionStore.Load();

        if (wasCrash && lastSession is not null && lastSession.Windows.Count > 0)
        {
            lastSession.IsCrashRecovery = true;
            SessionStore.SaveCrashSession(lastSession);
            _pendingRecoverySession = lastSession;
        }
        else if (SessionStore.HasCrashSession())
        {
            var savedCrash = SessionStore.LoadCrashSession();
            if (savedCrash is not null && savedCrash.Windows.Count > 0)
            {
                _pendingRecoverySession = savedCrash;
            }
        }

        _snapshotEngine = new SnapshotEngine(_settings);
        _tray = new TrayIconService(OpenRecovery, OpenSettings, ExitApp);

        // Immediate capture so even a quick crash has fresh data; but only after
        // we've loaded and preserved the recovery session
        _snapshotEngine.CaptureNow();

        if (_pendingRecoverySession is not null && _pendingRecoverySession.Windows.Count > 0)
        {
            Dispatcher.BeginInvoke(() => ShowRecovery(_pendingRecoverySession));
        }
        else if (lastSession is not null && lastSession.Windows.Count > 0)
        {
            _tray.UpdateTooltip($"{Constants.AppName}\nLast session: {lastSession.Windows.Count} apps");
        }

        Logger.Log($"Started. Crash recovery: {wasCrash}, last session apps: {lastSession?.Windows.Count ?? 0}");
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        // System shutdown/restart: take a final snapshot so the next boot
        // restores the freshest window positions. Sentinel stays absent so
        // recovery is offered after restart (by design).
        _snapshotEngine?.CaptureNow();
        Logger.Log("Session ending (system shutdown/restart).");
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _snapshotEngine?.CaptureNow();
        if (_cleanExit)
            CrashDetector.MarkCleanShutdown();
        _snapshotEngine?.Dispose();
        _tray?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private void OpenRecovery()
    {
        if (_recoveryWindow is { IsLoaded: true })
        {
            _recoveryWindow.Activate();
            return;
        }

        var snapshot = _pendingRecoverySession
            ?? SessionStore.LoadCrashSession()
            ?? SessionStore.Load();

        if (snapshot is null || snapshot.Windows.Count == 0)
        {
            _tray?.ShowBalloon("No session found", "No previous session snapshot is available yet.",
                Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
            return;
        }

        ShowRecovery(snapshot);
    }

    private void ShowRecovery(SessionSnapshot snapshot)
    {
        if (_recoveryWindow is { IsLoaded: true })
        {
            _recoveryWindow.Activate();
            return;
        }

        _recoveryWindow = new RecoveryWindow(snapshot, _settings);
        _recoveryWindow.SessionResolved += () =>
        {
            _pendingRecoverySession = null;
        };
        _recoveryWindow.Closed += (_, _) => _recoveryWindow = null;
        _recoveryWindow.Show();
        _recoveryWindow.Activate();
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, OnSettingsSaved);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnSettingsSaved(AppSettings newSettings)
    {
        _settings = newSettings;
        _snapshotEngine?.UpdateSettings(newSettings);
        Logger.Log($"Settings saved. Interval={newSettings.SnapshotIntervalSeconds}s");
    }

    private void ExitApp()
    {
        _cleanExit = true;
        _snapshotEngine?.CaptureNow();
        Shutdown();
    }
}
