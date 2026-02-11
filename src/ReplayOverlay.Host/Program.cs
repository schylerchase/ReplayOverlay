using System.Diagnostics;
using ReplayOverlay.Host.Services;

namespace ReplayOverlay.Host;

public static class Program
{
    private const string MutexName = "Global\\ReplayOverlay_SingleInstance";

    [STAThread]
    public static int Main(string[] args)
    {
        // Single-instance check
        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            Debug.WriteLine("Another instance is already running.");
            return 1;
        }

        // Admin elevation if configured
        var configService = new ConfigService();
        var config = configService.Load();
        if (config.RunAsAdmin && !AdminService.IsAdmin())
        {
            if (AdminService.RequestElevation())
                return 0; // new elevated process started
            // Fall through if elevation failed
        }

        // Launch WPF application
        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
