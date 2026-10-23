namespace SysScope;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var demo = args.Any(static arg => arg.Equals("--demo", StringComparison.OrdinalIgnoreCase));
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(new AppServices(demo)));
    }
}
