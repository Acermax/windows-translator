using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace WindowsTranslator.App.Features.Tray;

internal sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;

    public TrayIconService()
    {
        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add("Traducir seleccion", null, (_, _) => TranslateRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add("Ajustes", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("Salir", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _menu,
            Icon = Drawing.SystemIcons.Application,
            Text = "Windows Translator",
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => TranslateRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? TranslateRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public void ShowInfo(string title, string message)
    {
        _notifyIcon.ShowBalloonTip(3_000, title, message, Forms.ToolTipIcon.Info);
    }

    public void ShowError(string title, string message)
    {
        _notifyIcon.ShowBalloonTip(5_000, title, message, Forms.ToolTipIcon.Error);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
    }
}
