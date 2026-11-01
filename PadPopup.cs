using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Xaml = Microsoft.UI.Xaml;
using Brush = Microsoft.UI.Xaml.Media.Brush;
using Orientation = Microsoft.UI.Xaml.Controls.Orientation;
using ProgressBar = Microsoft.UI.Xaml.Controls.ProgressBar;

namespace PadBattery;

sealed class PadPopup : Xaml.Window
{
    const int PanelWidth = 320, Gap = 12, RowHeight = 52, RowSpacing = 8, TitleBar = 32, Inset = 16;

    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);

    sealed record RowParts(ProgressBar Bar, TextBlock Percent, FontIcon Bolt);

    readonly StackPanel rows = new() { Spacing = RowSpacing };
    readonly List<RowParts> parts = new();
    readonly DispatcherQueueTimer clickWatch;
    readonly IntPtr hwnd;
    string layout = "?";

    public bool IsClosed { get; private set; }


    public PadPopup()
    {
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        SystemBackdrop = new DesktopAcrylicBackdrop();
        Title = "Controles";
        ExtendsContentIntoTitleBar = true;

        var root = new StackPanel();
        root.Children.Add(new TextBlock
        {
            Text = "Controles",
            Style = TextStyle("BodyStrongTextBlockStyle"),
            Height = TitleBar,
            Padding = new Xaml.Thickness(Inset, 7, 0, 0),
        });
        rows.Padding = new Xaml.Thickness(Inset, 4, Inset, Inset);
        root.Children.Add(rows);
        Content = root;

        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        presenter.SetBorderAndTitleBar(true, true);
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;

        clickWatch = DispatcherQueue.CreateTimer();
        clickWatch.Interval = TimeSpan.FromMilliseconds(30);
        clickWatch.Tick += (_, _) => CloseOnClickOutside();

        Activated += (_, e) =>
        {
            if (e.WindowActivationState == Xaml.WindowActivationState.Deactivated) Hide();
        };
        Closed += (_, _) =>
        {
            IsClosed = true;
            clickWatch.Stop();
        };
    }

    public void Show(List<PadStatus> pads)
    {
        Update(pads);
        Activate();
        SetForegroundWindow(hwnd);
        GetAsyncKeyState(0x01);
        GetAsyncKeyState(0x02);
        clickWatch.Start();
    }

    public void Hide()
    {
        if (!IsClosed) Close();
    }

    void CloseOnClickOutside()
    {
        bool pressed = (GetAsyncKeyState(0x01) & 0x8001) != 0 || (GetAsyncKeyState(0x02) & 0x8001) != 0;
        if (!pressed) return;
        var cursor = System.Windows.Forms.Cursor.Position;
        var pos = AppWindow.Position;
        var size = AppWindow.Size;
        bool inside = cursor.X >= pos.X && cursor.Y >= pos.Y && cursor.X < pos.X + size.Width && cursor.Y < pos.Y + size.Height;
        if (!inside) Hide();
    }

    public void Update(List<PadStatus> pads)
    {
        string key = string.Join("|", pads.Select(p => p.Name));
        if (key == layout)
        {
            for (int i = 0; i < pads.Count; i++)
            {
                if (parts[i].Bar.Value != pads[i].Percent) parts[i].Bar.Value = pads[i].Percent;
                parts[i].Percent.Text = $"{pads[i].Percent}%";
                parts[i].Bolt.Visibility = pads[i].Charging ? Xaml.Visibility.Visible : Xaml.Visibility.Collapsed;
            }
            return;
        }

        layout = key;
        parts.Clear();
        rows.Children.Clear();
        if (pads.Count == 0)
            rows.Children.Add(new TextBlock
            {
                Text = "Nenhum controle conectado",
                Foreground = (Brush)Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"],
                Height = RowHeight,
            });
        foreach (var pad in pads)
            rows.Children.Add(Row(pad));
        PlaceNearClock(Math.Max(1, pads.Count));
    }

    Grid Row(PadStatus pad)
    {
        var grid = new Grid { Height = RowHeight, ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = Xaml.GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new Xaml.GridLength(1, Xaml.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = Xaml.GridLength.Auto });

        grid.Children.Add(new FontIcon { Glyph = "", FontSize = 24, VerticalAlignment = Xaml.VerticalAlignment.Center });

        var bar = new ProgressBar { Value = pad.Percent, Maximum = 100 };
        var middle = new StackPanel { Spacing = 6, VerticalAlignment = Xaml.VerticalAlignment.Center };
        middle.Children.Add(new TextBlock { Text = pad.Name, TextTrimming = Xaml.TextTrimming.CharacterEllipsis });
        middle.Children.Add(bar);
        Grid.SetColumn(middle, 1);
        grid.Children.Add(middle);

        var bolt = new FontIcon
        {
            Glyph = "",
            FontSize = 14,
            Visibility = pad.Charging ? Xaml.Visibility.Visible : Xaml.Visibility.Collapsed,
        };
        var percent = new TextBlock
        {
            Text = $"{pad.Percent}%",
            Style = TextStyle("BodyStrongTextBlockStyle"),
            MinWidth = 40,
            TextAlignment = Xaml.TextAlignment.Right,
        };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = Xaml.VerticalAlignment.Center };
        right.Children.Add(bolt);
        right.Children.Add(percent);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        parts.Add(new RowParts(bar, percent, bolt));
        return grid;
    }

    void PlaceNearClock(int count)
    {
        double scale = GetDpiForWindow(hwnd) / 96.0;
        int height = TitleBar + 4 + count * RowHeight + (count - 1) * RowSpacing + Inset;
        int w = (int)(PanelWidth * scale), h = (int)(height * scale), gap = (int)(Gap * scale);
        var area = DisplayArea.Primary.WorkArea;
        AppWindow.MoveAndResize(new RectInt32(area.X + area.Width - w - gap, area.Y + area.Height - h - gap, w, h));
    }

    static Xaml.Style TextStyle(string key) => (Xaml.Style)Xaml.Application.Current.Resources[key];
}
