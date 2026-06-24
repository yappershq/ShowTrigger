using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Managers;

namespace ShowTrigger;

internal sealed class InterfaceBridge
{
    internal static InterfaceBridge Instance { get; private set; } = null!;

    internal string SharpPath { get; }
    internal string DllPath   { get; }

    internal IModSharp       ModSharp       { get; }
    internal IClientManager  ClientManager  { get; }
    internal IEntityManager  EntityManager  { get; }
    internal ILoggerFactory  LoggerFactory  { get; }

    public InterfaceBridge(string dllPath, string sharpPath, ISharedSystem sharedSystem, ILoggerFactory loggerFactory)
    {
        Instance = this;

        SharpPath = sharpPath;
        DllPath   = dllPath;

        ModSharp      = sharedSystem.GetModSharp();
        ClientManager = sharedSystem.GetClientManager();
        EntityManager = sharedSystem.GetEntityManager();
        LoggerFactory = loggerFactory;
    }
}
