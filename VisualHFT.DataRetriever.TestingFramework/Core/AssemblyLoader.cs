using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VisualHFT.Commons.Helpers;
using VisualHFT.Commons.Interfaces;
using VisualHFT.PluginManager;


namespace VisualHFT.DataRetriever.TestingFramework.Core
{
    public class AssemblyLoader
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static List<IDataRetrieverTestable> LoadDataRetrievers()
        {
            var translators = new List<IDataRetrieverTestable>();
            var errors = new List<string>();

            foreach (var file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll"))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(file);
                    foreach (var type in assembly.GetExportedTypes())
                    {
                        if (!type.IsAbstract && type.GetInterfaces().Contains(typeof(IDataRetrieverTestable)))
                        {
                            try
                            {
                                var plugin = Activator.CreateInstance(type) as IDataRetrieverTestable;
                                if (plugin != null && ValidatePlugin(plugin))
                                {
                                    translators.Add(plugin);
                                    log.Info($"Successfully loaded plugin: {plugin.GetType().Name}");
                                }
                            }
                            catch (Exception ex)
                            {
                                var error = $"Failed to create instance of {type.Name}: {ex.Message}";
                                errors.Add(error);
                                log.Error(error, ex);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    var error = $"Failed to load assembly {file}: {ex.Message}";
                    errors.Add(error);
                    log.Error(error, ex);
                }
            }

            if (errors.Any())
            {
                log.Warn($"Assembly loading completed with {errors.Count} errors.");
            }

            log.Info($"Loaded {translators.Count} data retriever plugins for testing.");
            return translators;
        }

        private static bool ValidatePlugin(IDataRetrieverTestable plugin)
        {
            try
            {
                // Basic validation - ensure the plugin can be cast to required interfaces
                var dataRetriever = plugin as IDataRetriever;
                var pluginInterface = plugin as IPlugin;

                if (dataRetriever == null)
                {
                    log.Warn($"Plugin {plugin.GetType().Name} does not implement IDataRetriever");
                    return false;
                }

                if (pluginInterface == null)
                {
                    log.Warn($"Plugin {plugin.GetType().Name} does not implement IPlugin");
                    return false;
                }

                // Validate plugin has basic required properties
                if (string.IsNullOrEmpty(pluginInterface.Name))
                {
                    log.Warn($"Plugin {plugin.GetType().Name} has empty Name property");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Error($"Failed to validate plugin {plugin.GetType().Name}: {ex.Message}", ex);
                return false;
            }
        }
    }
}
