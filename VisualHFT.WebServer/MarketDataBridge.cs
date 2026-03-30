using System.Text.Json;
using System.Threading.Channels;
using VisualHFT.Helpers;
using VisualHFT.Model;
using VisualHFT.WebServer.Models;

namespace VisualHFT.WebServer;

public class MarketDataBridge : IDisposable
{
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(MarketDataBridge));

    private readonly WebSocketBroadcaster _broadcaster;
    private readonly Channel<OrderBookDto> _orderBookChannel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<string, DateTime> _lastSentPerSymbol = new();
    private readonly TimeSpan _throttleInterval = TimeSpan.FromMilliseconds(100); // 10 Hz max

    public MarketDataBridge(WebSocketBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;

        _orderBookChannel = Channel.CreateBounded<OrderBookDto>(
            new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });

        HelperOrderBook.Instance.Subscribe(OnOrderBookUpdate);
        HelperTrade.Instance.Subscribe(OnTradeUpdate);
        HelperProvider.Instance.OnStatusChanged += OnProviderStatusChanged;

        _ = ProcessOrderBookChannelAsync(_cts.Token);

        log.Info("MarketDataBridge started — subscribed to OrderBook, Trade, Provider events.");
    }

    private void OnOrderBookUpdate(OrderBook book)
    {
        try
        {
            var dto = new OrderBookDto
            {
                Symbol = book.Symbol,
                ProviderId = book.ProviderID,
                ProviderName = book.ProviderName ?? "",
                MidPrice = book.MidPrice,
                Spread = book.Spread,
                Imbalance = book.ImbalanceValue,
                Sequence = book.Sequence,
                Timestamp = (book.LastUpdated ?? DateTime.UtcNow).ToString("o"),
                Bids = new List<BookLevelDto>(),
                Asks = new List<BookLevelDto>()
            };

            foreach (var bid in book.Bids)
            {
                if (bid.Price.HasValue && bid.Size.HasValue)
                    dto.Bids.Add(new BookLevelDto { Price = bid.Price.Value, Size = bid.Size.Value });
            }
            foreach (var ask in book.Asks)
            {
                if (ask.Price.HasValue && ask.Size.HasValue)
                    dto.Asks.Add(new BookLevelDto { Price = ask.Price.Value, Size = ask.Size.Value });
            }

            dto.BestBid = dto.Bids.Count > 0 ? dto.Bids[0].Price : 0;
            dto.BestAsk = dto.Asks.Count > 0 ? dto.Asks[0].Price : 0;

            _orderBookChannel.Writer.TryWrite(dto);
        }
        catch (Exception ex)
        {
            log.Error("Error mapping OrderBook to DTO", ex);
        }
    }

    private async Task ProcessOrderBookChannelAsync(CancellationToken ct)
    {
        await foreach (var dto in _orderBookChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                var now = DateTime.UtcNow;
                if (_lastSentPerSymbol.TryGetValue(dto.Symbol, out var lastSent)
                    && (now - lastSent) < _throttleInterval)
                {
                    continue; // throttled
                }

                _lastSentPerSymbol[dto.Symbol] = now;

                var msg = new WsMessage<OrderBookDto> { Type = "orderbook", Data = dto };
                var json = JsonSerializer.Serialize(msg);
                await _broadcaster.BroadcastAsync(json);
            }
            catch (Exception ex)
            {
                log.Error("Error broadcasting OrderBook", ex);
            }
        }
    }

    private void OnTradeUpdate(Trade trade)
    {
        try
        {
            var dto = new TradeDto
            {
                Symbol = trade.Symbol,
                Price = (double)trade.Price,
                Size = (double)trade.Size,
                IsBuy = trade.IsBuy ?? false,
                Timestamp = trade.Timestamp.ToString("o"),
                MarketMidPrice = trade.MarketMidPrice
            };

            var msg = new WsMessage<TradeDto> { Type = "trade", Data = dto };
            var json = JsonSerializer.Serialize(msg);
            _ = _broadcaster.BroadcastAsync(json);
        }
        catch (Exception ex)
        {
            log.Error("Error broadcasting Trade", ex);
        }
    }

    private void OnProviderStatusChanged(object? sender, Provider provider)
    {
        try
        {
            var dto = new ProviderDto
            {
                ProviderId = provider.ProviderCode,
                ProviderName = provider.ProviderName ?? "",
                Status = provider.Status.ToString(),
                LastUpdated = provider.LastUpdated.ToString("o")
            };

            var msg = new WsMessage<ProviderDto> { Type = "provider", Data = dto };
            var json = JsonSerializer.Serialize(msg);
            _ = _broadcaster.BroadcastAsync(json);
        }
        catch (Exception ex)
        {
            log.Error("Error broadcasting Provider status", ex);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        HelperOrderBook.Instance.Unsubscribe(OnOrderBookUpdate);
        HelperTrade.Instance.Unsubscribe(OnTradeUpdate);
        HelperProvider.Instance.OnStatusChanged -= OnProviderStatusChanged;
    }
}
