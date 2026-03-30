using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MarketConnectors.Gemini.Model
{
    public class MarketUpdate
    {
        // auction_events: Keep as List<object> if it can contain varied types,
        // or define a specific AuctionEvent class if the structure is known.
        // Since it's empty in the example, List<object> or even ignoring it might be fine.
        [JsonPropertyName("auction_events")]
        public List<object> AuctionEvents { get; set; }

        // changes: This is an array of arrays of strings ["side", "price", "quantity"]
        // Direct mapping results in List<List<string>>. Requires post-processing.
        [JsonPropertyName("changes")]
        public List<List<string>> Changes { get; set; }

        [JsonPropertyName("symbol")]
        public string Symbol { get; set; }

        // trades: Array of Trade objects
        [JsonPropertyName("trades")]
        public List<Trade> Trades { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        // --- Properties for Processed Data (to avoid re-processing later) ---
        // You might want to populate these after deserialization for easier use
        [JsonIgnore] // Don't try to deserialize this directly
        public List<ChangeEntry> ProcessedChanges { get; } = new List<ChangeEntry>();
    }

}
