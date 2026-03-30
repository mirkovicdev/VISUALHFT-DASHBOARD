using VisualHFT.Commons.Interfaces;
using System.ComponentModel;

namespace MarketConnectorTemplate
{
    /// <summary>
    /// Settings class for the template market connector.  Each field is
    /// categorized so it will appear neatly grouped in the VisualHFT
    /// configuration UI.  You can extend this class to include any
    /// additional exchange‑specific parameters (for example, API endpoint
    /// overrides, authentication scopes, etc.).
    /// </summary>
    public class TemplateSettings : ISetting
    {
        [Category("Connection")]
        [Description("API key used for authenticated requests (leave empty for public data).")]
        public string ApiKey { get; set; } = string.Empty;

        [Category("Connection")]
        [Description("API secret used for authenticated requests (leave empty for public data).")]
        public string ApiSecret { get; set; } = string.Empty;

        [Category("General")]
        [Description("Comma‑separated list of symbols to subscribe to.  Use native exchange notation (e.g. BTCUSDT).")]
        public string Symbols { get; set; } = "BTCUSDT";

        [Category("Market Data")]
        [Description("Depth of the orderbook to request.  For example, 10 requests the top 10 bids/asks.")]
        public int DepthLevels { get; set; } = 10;

        [Category("Performance")]
        [Description("Aggregation window in milliseconds for orderbook events.  0 means no aggregation.")]
        public int AggregationLevelMs { get; set; } = 0;
    }
}