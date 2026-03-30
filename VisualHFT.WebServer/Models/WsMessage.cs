using System.Text.Json.Serialization;

namespace VisualHFT.WebServer.Models;

public class WsMessage<T>
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("data")]
    public T? Data { get; set; }
}

public class BookLevelDto
{
    [JsonPropertyName("price")]
    public double Price { get; set; }

    [JsonPropertyName("size")]
    public double Size { get; set; }
}

public class OrderBookDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = "";

    [JsonPropertyName("providerId")]
    public int ProviderId { get; set; }

    [JsonPropertyName("providerName")]
    public string ProviderName { get; set; } = "";

    [JsonPropertyName("midPrice")]
    public double MidPrice { get; set; }

    [JsonPropertyName("spread")]
    public double Spread { get; set; }

    [JsonPropertyName("imbalance")]
    public double Imbalance { get; set; }

    [JsonPropertyName("bestBid")]
    public double BestBid { get; set; }

    [JsonPropertyName("bestAsk")]
    public double BestAsk { get; set; }

    [JsonPropertyName("sequence")]
    public long Sequence { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("bids")]
    public List<BookLevelDto> Bids { get; set; } = new();

    [JsonPropertyName("asks")]
    public List<BookLevelDto> Asks { get; set; } = new();
}

public class TradeDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = "";

    [JsonPropertyName("price")]
    public double Price { get; set; }

    [JsonPropertyName("size")]
    public double Size { get; set; }

    [JsonPropertyName("isBuy")]
    public bool IsBuy { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("marketMidPrice")]
    public double MarketMidPrice { get; set; }
}

public class StudyDto
{
    [JsonPropertyName("studyName")]
    public string StudyName { get; set; } = "";

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = "";

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("format")]
    public string Format { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("valueColor")]
    public string? ValueColor { get; set; }

    [JsonPropertyName("marketMidPrice")]
    public double MarketMidPrice { get; set; }

    [JsonPropertyName("hasData")]
    public bool HasData { get; set; }

    [JsonPropertyName("hasError")]
    public bool HasError { get; set; }

    [JsonPropertyName("isStale")]
    public bool IsStale { get; set; }
}

public class ProviderDto
{
    [JsonPropertyName("providerId")]
    public int ProviderId { get; set; }

    [JsonPropertyName("providerName")]
    public string ProviderName { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("lastUpdated")]
    public string LastUpdated { get; set; } = "";
}
