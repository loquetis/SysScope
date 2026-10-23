using SysScope.Core.Collection;
using SysScope.Core.Demo;
using SysScope.Core.Registry;
using SysScope.Core.Wmi;

namespace SysScope;

public sealed class AppServices
{
    public AppServices(bool demo)
    {
        Demo = demo;
        if (demo)
        {
            Sessions = new DemoWmiSessionFactory();
            Registries = new DemoRegistryFactory();
        }
        else
        {
            Sessions = new FallbackConnector([new DcomTransport(), new WinRmTransport()]);
            Registries = new WindowsRegistryFactory();
        }

        Collector = new MachineCollector(Sessions, Registries);
        Scanner = new InventoryScanner(Collector, maxDegreeOfParallelism: 6);
    }

    public bool Demo { get; }
    public IWmiSessionFactory Sessions { get; }
    public IRegistryReaderFactory Registries { get; }
    public IMachineCollector Collector { get; }
    public InventoryScanner Scanner { get; }
}
