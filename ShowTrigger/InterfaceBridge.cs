using System;
using Microsoft.Extensions.Logging;
using Sharp.Modules.LocalizerManager.Shared;
using Sharp.Shared;
using Sharp.Shared.Definition;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;

namespace ShowTrigger;

internal sealed class InterfaceBridge
{
    internal static InterfaceBridge Instance { get; private set; } = null!;

    internal string SharpPath { get; }
    internal string DllPath   { get; }

    internal IModSharp           ModSharp           { get; }
    internal IClientManager      ClientManager      { get; }
    internal IEntityManager      EntityManager      { get; }
    internal ISharpModuleManager SharpModuleManager { get; }
    internal ILoggerFactory      LoggerFactory      { get; }

    internal ILocalizerManager? LocalizerManager { get; private set; }

    public InterfaceBridge(string dllPath, string sharpPath, ISharedSystem sharedSystem, ILoggerFactory loggerFactory)
    {
        Instance = this;

        SharpPath = sharpPath;
        DllPath   = dllPath;

        ModSharp           = sharedSystem.GetModSharp();
        ClientManager      = sharedSystem.GetClientManager();
        EntityManager      = sharedSystem.GetEntityManager();
        SharpModuleManager = sharedSystem.GetSharpModuleManager();
        LoggerFactory      = loggerFactory;
    }

    internal void InitLocalizer()
    {
        var iface = SharpModuleManager
            .GetOptionalSharpModuleInterface<ILocalizerManager>(ILocalizerManager.Identity);
        if (iface?.Instance is not { } lm)
            return;

        LocalizerManager = lm;
        try
        {
            lm.LoadLocaleFile("showtrigger", suppressDuplicationWarnings: true);
        }
        catch (Exception e)
        {
            LoggerFactory.CreateLogger<InterfaceBridge>()
                .LogWarning(e, "[ShowTrigger] showtrigger.json locale not found — using key fallbacks.");
        }
    }

    /// <summary>Localize a key for a client and resolve {{color}} tokens; falls back to the key.</summary>
    internal string Localize(IGameClient client, string key, params object?[] args)
    {
        if (LocalizerManager?.For(client) is not { } locale)
            return key;

        try
        {
            return ProcessColorCodes(locale.Text(key, args));
        }
        catch
        {
            return key;
        }
    }

    private static string ProcessColorCodes(string message)
    {
        if (string.IsNullOrEmpty(message) || !message.Contains('{'))
            return message;

        return message
            .Replace("{default}",   ChatColor.White,   StringComparison.OrdinalIgnoreCase)
            .Replace("{white}",     ChatColor.White,   StringComparison.OrdinalIgnoreCase)
            .Replace("{green}",     ChatColor.Green,   StringComparison.OrdinalIgnoreCase)
            .Replace("{darkred}",   ChatColor.DarkRed, StringComparison.OrdinalIgnoreCase)
            .Replace("{gold}",      ChatColor.Gold,    StringComparison.OrdinalIgnoreCase);
    }
}
