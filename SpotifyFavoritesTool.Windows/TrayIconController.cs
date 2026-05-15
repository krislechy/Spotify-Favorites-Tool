using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace SpotifyFavoritesTool;

public sealed class TrayIconController : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action _showMainWindow;
    private readonly Action _showSettings;
    private readonly Action _exitApplication;
    private readonly Forms.NotifyIcon _icon;

    public TrayIconController(
        Dispatcher dispatcher,
        Action showMainWindow,
        Action showSettings,
        Action exitApplication)
    {
        _dispatcher = dispatcher;
        _showMainWindow = showMainWindow;
        _showSettings = showSettings;
        _exitApplication = exitApplication;

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Spotify Favorites Tool",
            Visible = true,
            ContextMenuStrip = CreateMenu()
        };
        _icon.DoubleClick += (_, _) => Invoke(_showMainWindow);
    }

    public void Dispose()
    {
        _icon.Dispose();
    }

    private Forms.ContextMenuStrip CreateMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => Invoke(_showMainWindow));
        menu.Items.Add("Настройки", null, (_, _) => Invoke(_showSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => Invoke(_exitApplication));
        return menu;
    }

    private void Invoke(Action action)
    {
        _dispatcher.Invoke(action);
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            return System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty)
                ?? System.Drawing.SystemIcons.Application;
        }
        catch
        {
            return System.Drawing.SystemIcons.Application;
        }
    }
}
