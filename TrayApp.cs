using Microsoft.Win32;
using Windows.Gaming.Input;

namespace PadBattery;

sealed class TrayApp
{
    const int RefreshMilliseconds = 5000;
    const int ReopenGuardMilliseconds = 400;

    readonly NotifyIcon tray = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = RefreshMilliseconds };
    readonly SynchronizationContext ui;
    List<PadStatus> pads = new();
    Icon? icon;
    string iconKey = "";
    bool refreshing;
    PadPopup? popup;
    DateTime popupClosedAt;

    public TrayApp()
    {
        _ = Gamepad.Gamepads;

        var autoStart = new ToolStripMenuItem("Iniciar com o Windows") { Checked = AutoStart.Enabled, CheckOnClick = true };
        autoStart.CheckedChanged += (_, _) => AutoStart.Enabled = autoStart.Checked;
        var menu = new ContextMenuStrip();
        menu.Items.Add(autoStart);
        menu.Items.Add("Sair", null, (_, _) => Exit());
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) TogglePopup();
        };
        ui = SynchronizationContext.Current!;

        Render();
        tray.Visible = true;

        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        timer.Tick += async (_, _) => await Refresh();
        timer.Start();
        _ = Refresh();
    }

    async Task Refresh()
    {
        if (refreshing) return;
        refreshing = true;
        try
        {
            pads = await Task.Run(Controllers.Read);
            Render();
        }
        catch
        {
        }
        finally
        {
            refreshing = false;
        }
    }

    void Render()
    {
        var main = pads.FirstOrDefault();
        bool light = Theme.TaskbarIsLight();
        int size = SystemInformation.SmallIconSize.Width;
        string key = $"{main?.Percent}|{main?.Charging}|{light}|{size}";

        if (key != iconKey)
        {
            var old = icon;
            icon = PadIcon.Draw(size, main?.Percent, main?.Charging ?? false, light);
            tray.Icon = icon;
            if (old != null) PadIcon.Destroy(old);
            iconKey = key;
        }

        string text = pads.Count == 0 ? "Nenhum controle conectado" : string.Join("\n", pads);
        tray.Text = text.Length > 127 ? text[..127] : text;
        popup?.Update(pads);
    }

    void TogglePopup()
    {
        if (popup != null)
        {
            popup.Hide();
            return;
        }
        if ((DateTime.Now - popupClosedAt).TotalMilliseconds < ReopenGuardMilliseconds) return;

        popup = new PadPopup();
        popup.Closed += (_, _) =>
        {
            popup = null;
            popupClosedAt = DateTime.Now;
        };
        popup.Show(pads);
        _ = Refresh();
    }

    void OnPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e) => ui.Post(_ => Render(), null);

    void Exit()
    {
        SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
        timer.Stop();
        tray.Visible = false;
        tray.Dispose();
        if (icon != null) PadIcon.Destroy(icon);
        popup?.Hide();
        Microsoft.UI.Xaml.Application.Current.Exit();
    }
}
