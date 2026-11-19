using System.Drawing;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using WorkspaceRestorer.Models;

namespace WorkspaceRestorer.Services;

internal class TrayIconService : IDisposable
{
    private readonly TaskbarIcon _tray;
    private readonly Action _openRecovery;
    private readonly Action _openSettings;
    private readonly Action _exitApp;

    public TrayIconService(Action openRecovery, Action openSettings, Action exitApp)
    {
        _openRecovery = openRecovery;
        _openSettings = openSettings;
        _exitApp = exitApp;

        _tray = new TaskbarIcon
        {
            ToolTipText = Constants.AppName,
            Visibility = Visibility.Visible,
            ContextMenu = BuildMenu()
        };

        // Draw a simple tray icon programmatically
        _tray.Icon = CreateIcon();
        _tray.TrayMouseDoubleClick += (_, _) => _openRecovery();
    }

    private System.Windows.Controls.ContextMenu BuildMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var restore = new System.Windows.Controls.MenuItem { Header = "Restore Last Session…" };
        restore.Click += (_, _) => _openRecovery();
        menu.Items.Add(restore);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var settings = new System.Windows.Controls.MenuItem { Header = "Settings" };
        settings.Click += (_, _) => _openSettings();
        menu.Items.Add(settings);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exit = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exit.Click += (_, _) => _exitApp();
        menu.Items.Add(exit);

        return menu;
    }

    public void ShowBalloon(string title, string message, BalloonIcon icon = BalloonIcon.Info)
    {
        _tray.ShowBalloonTip(title, message, icon);
    }

    public void UpdateTooltip(string text) => _tray.ToolTipText = text;

    private static Icon CreateIcon()
    {
        // 32x32 blue rounded square with white circular arrow hint (simplified)
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(Color.FromArgb(30, 100, 200));
            g.FillEllipse(brush, 2, 2, 28, 28);

            using var pen = new Pen(Color.White, 3)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };
            // Draw a partial arc as a "restore" loop
            g.DrawArc(pen, 8, 8, 16, 16, -30, 240);

            // Arrow head
            var ax = 8 + 16 * (float)Math.Cos(-30 * Math.PI / 180);
            var ay = 8 + 16 * (float)Math.Sin(-30 * Math.PI / 180);
            g.DrawLine(pen, ax, ay, ax - 5, ay - 1);
            g.DrawLine(pen, ax, ay, ax - 1, ay + 5);
        }

        var hIcon = bmp.GetHicon();
        try
        {
            return Icon.FromHandle(hIcon);
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    public void Dispose()
    {
        _tray?.Dispose();
    }
}
