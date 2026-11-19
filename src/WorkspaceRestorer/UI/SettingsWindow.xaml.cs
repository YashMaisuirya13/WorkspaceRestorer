using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WorkspaceRestorer.Services;

namespace WorkspaceRestorer.UI;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action<AppSettings> _onSaved;

    public string DataPath => Constants.DataDirectory;

    public SettingsWindow(AppSettings settings, Action<AppSettings> onSaved)
    {
        InitializeComponent();
        _settings = settings;
        _onSaved = onSaved;
        DataContext = this;

        IntervalBox.Text = settings.SnapshotIntervalSeconds.ToString();
        StartWithWindowsBox.IsChecked = settings.StartWithWindows;
        SkipMinimizedBox.IsChecked = settings.SkipMinimizedWindows;
        RestoreMaximizedBox.IsChecked = settings.RestoreMaximizedAsMaximized;
        DelayBox.Text = settings.LaunchDelayMs.ToString();
    }

    private int ParseClamped(TextBox box, int min, int max, int fallback)
    {
        if (int.TryParse(box.Text.Trim(), out var value))
            return Math.Clamp(value, min, max);
        return fallback;
    }

    private void Interval_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            IntervalBox.Text = ParseClamped(IntervalBox, Constants.MinSnapshotIntervalSeconds, Constants.MaxSnapshotIntervalSeconds, _settings.SnapshotIntervalSeconds).ToString();
        }
    }

    private void Interval_LostFocus(object sender, RoutedEventArgs e)
    {
        IntervalBox.Text = ParseClamped(IntervalBox, Constants.MinSnapshotIntervalSeconds, Constants.MaxSnapshotIntervalSeconds, _settings.SnapshotIntervalSeconds).ToString();
    }

    private void Delay_LostFocus(object sender, RoutedEventArgs e)
    {
        DelayBox.Text = ParseClamped(DelayBox, 0, 5000, _settings.LaunchDelayMs).ToString();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Constants.DataDirectory);
        Process.Start(new ProcessStartInfo { FileName = Constants.DataDirectory, UseShellExecute = true });
    }

    private void ClearSession_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show("Delete the saved session snapshot?", "Clear Session",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.Yes)
        {
            SessionStore.Delete();
            MessageBox.Show("Session cleared.", "Clear Session", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.SnapshotIntervalSeconds = ParseClamped(IntervalBox, Constants.MinSnapshotIntervalSeconds, Constants.MaxSnapshotIntervalSeconds, _settings.SnapshotIntervalSeconds);
        _settings.StartWithWindows = StartWithWindowsBox.IsChecked == true;
        _settings.SkipMinimizedWindows = SkipMinimizedBox.IsChecked == true;
        _settings.RestoreMaximizedAsMaximized = RestoreMaximizedBox.IsChecked == true;
        _settings.LaunchDelayMs = ParseClamped(DelayBox, 0, 5000, _settings.LaunchDelayMs);

        SettingsStore.Save(_settings);

        try
        {
            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "WorkspaceRestorer.exe");
            StartupManager.SetEnabled(_settings.StartWithWindows, exe);
        }
        catch (Exception ex)
        {
            Logger.Log($"Startup toggle failed: {ex.Message}");
            MessageBox.Show($"Failed to update startup registration:\n{ex.Message}", "Settings",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _onSaved(_settings);
        Close();
    }
}
