namespace PadBattery;

public partial class App : Microsoft.UI.Xaml.Application
{
    TrayApp? tray;

    public App()
    {
        InitializeComponent();
        DispatcherShutdownMode = Microsoft.UI.Xaml.DispatcherShutdownMode.OnExplicitShutdown;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) => tray = new TrayApp();
}
