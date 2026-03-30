using System.Reflection;
using VisualHFT.Commons.Helpers;
using VisualHFT.Commons.Studies;
using VisualHFT.DataRetriever;
using VisualHFT.Enums;
using VisualHFT.PluginManager;
using VisualHFT.UserSettings;

namespace VisualHFT.WebServer;

public class HeadlessPluginLoader
{
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(HeadlessPluginLoader));
    private readonly List<IPlugin> _plugins = new();

    public IReadOnlyList<IPlugin> AllPlugins => _plugins;

    public void LoadPlugins(string pluginsDirectory)
    {
        log.Info($"Loading plugins from: {pluginsDirectory}");

        foreach (var file in Directory.GetFiles(pluginsDirectory, "*.dll"))
        {
            try
            {
                var assembly = Assembly.LoadFrom(file);
                foreach (var type in assembly.GetExportedTypes())
                {
                    if (!type.IsAbstract && type.GetInterfaces().Contains(typeof(IPlugin)))
                    {
                        try
                        {
                            var plugin = Activator.CreateInstance(type) as IPlugin;
                            if (plugin == null || string.IsNullOrEmpty(plugin.Name))
                                continue;
                            if (!LicenseManager.Instance.HasAccess(plugin.RequiredLicenseLevel))
                                continue;

                            plugin.Status = ePluginStatus.LOADING;
                            _plugins.Add(plugin);
                            log.Info($"Plugin loaded: {plugin.Name} ({plugin.PluginType})");
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Failed to instantiate plugin from {file}: {type.FullName}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug($"Skipped non-plugin DLL: {Path.GetFileName(file)} ({ex.GetType().Name})");
            }
        }

        log.Info($"Total plugins loaded: {_plugins.Count}");
    }

    /// <summary>
    /// Configure study plugins with the correct symbol and provider so their
    /// internal filters match incoming market data. Without this, studies drop
    /// all data because their defaults are Symbol="" and ProviderID=0.
    /// </summary>
    public void ConfigureStudies(string symbol, int providerId, string providerName)
    {
        Console.WriteLine($"[..] Configuring studies for {symbol} / {providerName} (ID:{providerId})");

        foreach (var plugin in _plugins)
        {
            if (plugin is IStudy || plugin is IMultiStudy)
            {
                var settings = plugin.Settings;
                if (settings == null) continue;

                bool changed = false;

                if (string.IsNullOrEmpty(settings.Symbol) || settings.Symbol != symbol)
                {
                    settings.Symbol = symbol;
                    changed = true;
                }

                if (settings.Provider == null)
                {
                    settings.Provider = new VisualHFT.Model.Provider
                    {
                        ProviderID = providerId,
                        ProviderName = providerName
                    };
                    changed = true;
                }
                else if (settings.Provider.ProviderID != providerId)
                {
                    settings.Provider.ProviderID = providerId;
                    settings.Provider.ProviderName = providerName;
                    changed = true;
                }

                if (changed)
                {
                    Console.WriteLine($"     Configured: {plugin.Name} -> {symbol} / {providerName}");
                }
            }
        }
    }

    public async Task StartPluginsAsync()
    {
        var tasks = new List<Task>();

        foreach (var plugin in _plugins)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    if (plugin is IDataRetriever dataRetriever)
                    {
                        await dataRetriever.StartAsync();
                        log.Info($"Started data retriever: {plugin.Name}");
                    }
                    else if (plugin is IStudy study)
                    {
                        await study.StartAsync();
                        log.Info($"Started study: {plugin.Name}");
                    }
                    else if (plugin is IMultiStudy multiStudy)
                    {
                        await multiStudy.StartAsync();
                        log.Info($"Started multi-study: {plugin.Name}");
                    }
                }
                catch (Exception ex)
                {
                    plugin.Status = ePluginStatus.STOPPED_FAILED;
                    log.Error($"Plugin failed to start: {plugin.Name}", ex);
                }
            }));
        }

        await Task.WhenAll(tasks);
        log.Info("All plugins started.");
    }

    public void UnloadPlugins()
    {
        foreach (var plugin in _plugins.OfType<IDisposable>())
        {
            try { plugin.Dispose(); }
            catch (Exception ex) { log.Error($"Error disposing plugin", ex); }
        }
        _plugins.Clear();
    }
}
