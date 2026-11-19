using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WorkspaceRestorer.Models;
using WorkspaceRestorer.Services;

namespace WorkspaceRestorer.UI;

public partial class RecoveryWindow : Window
{
    private readonly SessionSnapshot _snapshot;
    private readonly AppSettings _settings;
    private readonly ObservableCollection<AppItemViewModel> _items;

    public event Action? SessionResolved;

    public RecoveryWindow(SessionSnapshot snapshot, AppSettings settings)
    {
        InitializeComponent();
        _snapshot = snapshot;
        _settings = settings;

        HeaderTitleText.Text = snapshot.IsCrashRecovery ? "Unclean shutdown detected" : "Restore Workspace Session";

        _items = new ObservableCollection<AppItemViewModel>(
            snapshot.Windows.Select(w => new AppItemViewModel(w)));

        AppList.ItemsSource = _items;
        SubtitleText.Text = $"Last snapshot at {snapshot.Timestamp:HH:mm:ss} · {snapshot.Windows.Count} application(s) captured";
        UpdateCount();

        foreach (var item in _items)
            item.PropertyChanged += Item_Changed;
    }

    private void UpdateCount()
    {
        var selected = _items.Count(i => i.IsSelected);
        CountText.Text = $"{selected} of {_items.Count} selected";
        RestoreButton.IsEnabled = selected > 0;
    }

    private void Item_Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppItemViewModel.IsSelected))
        {
            UpdateCount();
            var all = _items.All(i => i.IsSelected);
            SelectAllBox.Checked -= SelectAll_Changed;
            SelectAllBox.Unchecked -= SelectAll_Changed;
            SelectAllBox.IsChecked = all;
            SelectAllBox.Checked += SelectAll_Changed;
            SelectAllBox.Unchecked += SelectAll_Changed;
        }
    }

    private void SelectAll_Changed(object sender, RoutedEventArgs e)
    {
        var check = SelectAllBox.IsChecked == true;
        foreach (var item in _items)
            item.IsSelected = check;
        UpdateCount();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Discard();
        }
    }

    private void Discard_Click(object sender, RoutedEventArgs e) => Discard();

    private void Discard()
    {
        SessionStore.ClearCrashSession();
        SessionResolved?.Invoke();
        Close();
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var selected = _items.Where(i => i.IsSelected).Select(i => i.Window).ToList();
        if (selected.Count == 0) return;

        RestoreButton.IsEnabled = false;
        DiscardButton.IsEnabled = false;
        StatusText.Text = $"Restoring {selected.Count} application(s)…";

        var result = await Task.Run(() => RestorationEngine.Restore(selected, _settings));

        SessionStore.ClearCrashSession();
        SessionResolved?.Invoke();

        var message = result.Failures.Count == 0
            ? $"Restored {result.Succeeded} of {result.Requested} window(s)."
            : $"Restored {result.Succeeded} of {result.Requested}. Failures: {string.Join("; ", result.Failures)}";

        StatusText.Text = message;
        Logger.Log(message);

        // Brief delay so user sees completion, then close
        await Task.Delay(1500);
        Close();
    }
}

public class AppItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public AppWindow Window { get; }
    public string DisplayName { get; }
    public string DetailText { get; }
    public string SizeText { get; }
    public ImageSource? Icon { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public AppItemViewModel(AppWindow window)
    {
        Window = window;
        DisplayName = string.IsNullOrWhiteSpace(window.ProcessName) ? window.Title : window.ProcessName;
        DetailText = window.Title;
        var width = window.Right - window.Left;
        var height = window.Bottom - window.Top;
        SizeText = $"{width}×{height}";
        Icon = LoadIcon(window.ExePath);
    }

    private static ImageSource? LoadIcon(string exePath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (sysIcon != null)
                {
                    var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        sysIcon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
            }
        }
        catch { }

        try
        {
            var fallback = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                System.Drawing.SystemIcons.Application.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            fallback.Freeze();
            return fallback;
        }
        catch
        {
            return null;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
