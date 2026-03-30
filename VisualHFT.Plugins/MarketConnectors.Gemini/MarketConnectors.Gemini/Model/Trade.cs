using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MarketConnectors.Gemini.Model
{
    public class Trade
    {
        [JsonPropertyName("event_id")]
        public long EventId { get; set; }

        // Use decimal for price/quantity if precision is needed.
        // System.Text.Json needs help reading numbers-as-strings directly into decimal.
        [JsonPropertyName("price")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] // Requires .NET 5+
        public decimal Price { get; set; }

        [JsonPropertyName("quantity")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] // Requires .NET 5+
        public decimal Quantity { get; set; }

        // Could use an enum for Side for better type safety & potentially less string allocation
        [JsonPropertyName("side")]
        public string Side { get; set; }
        // Example Enum: public enum OrderSide { buy, sell }
        // [JsonPropertyName("side")]
        // public OrderSide Side { get; set; } // Requires enum converter or strings matching enum names

        [JsonPropertyName("symbol")]
        public string Symbol { get; set; }

        [JsonPropertyName("tid")]
        public long Tid { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

}
