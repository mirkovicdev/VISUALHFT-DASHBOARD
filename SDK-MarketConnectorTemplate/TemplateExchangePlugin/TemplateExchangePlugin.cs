using System;
using System.Threading.Tasks;
using VisualHFT.Commons.Helpers;
using VisualHFT.Enums;
using VisualHFT.Model;
using VisualHFT.PluginManager;
using VisualHFT.UserSettings;

namespace TemplateExchangePlugin
{
    /// <summary>
    /// Template for a market connector plugin. Implement IPlugin and derive from BasePluginDataRetriever
    /// to receive market data and push it into the VisualHFT helper classes.
    /// </summary>
    public class TemplateExchangePlugin : VisualHFT.Commons.PluginManager.BasePluginDataRetriever
    {
        public TemplateExchangePlugin()
        {
            Name = "TemplateExchange";
            Description = "Connects to TemplateExchange and streams order book and trade data.";
            Author = "Developer Name";
            Version = "0.0.1";
            Settings = new TemplateExchangeSettings();
        }

        /// <summary>
        /// Start the plugin asynchronously. Initialize connections to the exchange API here.
        /// Use Settings values such as API keys, symbols, depth levels, etc.
        /// </summary>
        public override async Task StartAsync()
        {
            Status = ePluginStatus.STARTING;
            // TODO: Initialize your API client here using Settings.ApiKey, Settings.ApiSecret, etc.
            // Subscribe to order book updates and trades. For each update call RaiseOnDataReceived with
            // the appropriate OrderBook or Trade model. Set provider status via RaiseOnProviderStatusChanged.
            Status = ePluginStatus.STARTED;
        }

        /// <summary>
        /// Stop the plugin asynchronously. Close connections and clean up resources.
        /// </summary>
        public override async Task StopAsync()
        {
            Status = ePluginStatus.STOPPING;
            // TODO: Disconnect from the exchange API and dispose of resources.
            Status = ePluginStatus.STOPPED;
        }

        /// <summary>
        /// Initialize default settings for the plugin. Invoked on construction.
        /// </summary>
        protected override void InitializeDefaultSettings()
        {
            Settings = new TemplateExchangeSettings
            {
                ApiKey = string.Empty,
                ApiSecret = string.Empty,
                Symbols = "BTC-USD",  // comma-separated list of symbols
                DepthLevels = 20
            };
        }
    }
}
