using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sharp.Shared;

namespace ShowTrigger;

/// <summary>
/// ShowTrigger — in-world trigger-zone visualizer for ModSharp / CS2.
/// Toggles the engine debug-overlay bits (name + bounding box) on every trigger_* entity and
/// broadcasts them to clients via sv_debug_overlays_broadcast. Per-player toggle: !showtriggers.
/// See docs/MECHANISM.md.
/// </summary>
public sealed class ShowTriggerPlugin : IModSharpModule
{
    public string DisplayName   => "ShowTrigger";
    public string DisplayAuthor => "yappershq";

    private readonly ILogger<ShowTriggerPlugin> _logger;
    private readonly ServiceProvider            _serviceProvider;

    public ShowTriggerPlugin(
        ISharedSystem   sharedSystem,
        string?         dllPath,
        string?         sharpPath,
        Version?        version,
        IConfiguration? coreConfiguration,
        bool            hotReload)
    {
        ArgumentNullException.ThrowIfNull(sharedSystem);
        ArgumentNullException.ThrowIfNull(dllPath);
        ArgumentNullException.ThrowIfNull(sharpPath);

        var loggerFactory = sharedSystem.GetLoggerFactory();
        _logger = loggerFactory.CreateLogger<ShowTriggerPlugin>();

        _ = new InterfaceBridge(dllPath, sharpPath, sharedSystem, loggerFactory);

        var services = new ServiceCollection();
        services.AddSingleton(sharedSystem);
        services.AddSingleton(InterfaceBridge.Instance);
        services.AddSingleton<ILoggerFactory>(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(LoggerFactoryLogger<>));
        services.AddModules();

        _serviceProvider = services.BuildServiceProvider();
    }

    public bool Init()
    {
        foreach (var module in _serviceProvider.GetServices<IModule>())
            CallSafe(module, static m => m.Init(), "Init");

        return true;
    }

    public void PostInit()
    {
        foreach (var module in _serviceProvider.GetServices<IModule>())
            CallSafe(module, static m => m.OnPostInit(), "PostInit");
    }

    public void OnAllModulesLoaded()
    {
        foreach (var module in _serviceProvider.GetServices<IModule>())
            CallSafe(module, static m => m.OnAllSharpModulesLoaded(), "OnAllModulesLoaded");

        _logger.LogInformation("[ShowTrigger] Plugin loaded successfully");
    }

    public void Shutdown()
    {
        foreach (var module in _serviceProvider.GetServices<IModule>())
            CallSafe(module, static m => m.Shutdown(), "Shutdown");

        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();
    }

    public void OnLibraryConnected(string name)  { }
    public void OnLibraryDisconnect(string name) { }

    private void CallSafe(IModule module, Action<IModule> action, string phase)
    {
        try
        {
            action(module);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ShowTrigger] Error in {Phase} for {Module}", phase, module.GetType().Name);
        }
    }
}

internal sealed class LoggerFactoryLogger<T>(ILoggerFactory factory) : ILogger<T>
{
    private readonly ILogger _inner = factory.CreateLogger(typeof(T).Name);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);
    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _inner.Log(logLevel, eventId, state, exception, formatter);
}
