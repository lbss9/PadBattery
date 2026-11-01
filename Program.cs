using Microsoft.UI.Dispatching;

namespace PadBattery;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "PadBattery.Single", out bool first);
        if (!first) return;

        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(_ =>
        {
            var queue = DispatcherQueue.GetForCurrentThread();
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(queue));
            new App();
        });
    }
}

record PadStatus(string Name, int Percent, bool Charging)
{
    public override string ToString() => $"{Name}: {Percent}%{(Charging ? " (carregando)" : "")}";
}
