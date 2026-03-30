using System.ComponentModel;
using VisualHFT.Enums;
using VisualHFT.UserSettings;

namespace TemplateExchangePlugin
{
    /// <summary>
    /// Settings class for TemplateExchange plugin. Extend ISetting to expose
    /// configurable properties that the user can set via the settings UI.
    /// </summary>
    public class TemplateExchangeSettings : ISetting
    {
        [Description("API key issued by the exchange")]
        public string ApiKey { get; set; }

        [Description("API secret issued by the exchange")]
        public string ApiSecret { get; set; }

        [Description("Symbols to subscribe, comma-separated (e.g., BTC-USD,ETH-USD)")]
        public string Symbols { get; set; }

        [Description("Depth levels to request for the order book")]
        public int DepthLevels { get; set; }

        [Description("Aggregation level for studies (time in ms or event count)")]
        public int AggregationLevel { get; set; } = 1;

        [Description("License level required to use this plugin")]
        public eLicenseLevel LicenseLevel { get; set; } = eLicenseLevel.COMMUNITY;
    }
}
