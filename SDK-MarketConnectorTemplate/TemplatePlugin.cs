using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VisualHFT.Commons.PluginManager;
using VisualHFT.Enums;
using VisualHFT.Commons.Model;
using VisualHFT.Commons.Interfaces;

namespace MarketConnectorTemplate
{
    /// <summary>
    /// Skeleton implementation of a market data connector.  To build a real
    /// connector, derive from BasePluginDataRetriever and implement the
    /// connection and subscription logic using your chosen exchange client
    /// library.  This class provides overridable methods for starting and
    /// stopping the plug‑in and illustrates how to publish orderbook and
    /// trade events into VisualHFT.
    /// </summary>
    public class TemplatePlugin : BasePluginDataRetriever
    {
        private TemplateSettings _settings;

        /// <inheritdoc />
        public override string Name { get; set; } = "Template Exchange Plugin";
        /// <inheritdoc />
        public override string Version { get; set; } = "0.1.0";
        /// <inheritdoc />
        public override string Description { get; set; } = "Skeleton connector for a generic exchange.";
        /// <inheritdoc />
        public override string Author { get; set; } = "VisualHFT Community";
        /// <inheritdoc />
        public override ISetting Settings { get => _settings; set => _settings = (TemplateSettings)value; }
        /// <inheritdoc />
        public override Action CloseSettingWindow { get; set; }

        public TemplatePlugin()
        {
            // Set the reconnection action to automatically restart the connector
            // when a connection drop is detected.  If your exchange client
            // exposes reconnection hooks you may override this to use them instead.
            SetReconnectionAction(InternalStartAsync);
        }

        /// <summary>
        /// Called by the plug‑in manager when the user starts this plug‑in.
        /// Establishes the connection to the exchange and subscribes to
        /// orderbook and trade feeds for the configured symbols.  Always
        /// call base.StartAsync() before performing your own logic so that
        /// VisualHFT can update the provider status correctly.
        /// </summary>
        public override async Task StartAsync()
        {
            await base.StartAsync();

            // TODO: Instantiate your exchange client here using _settings.  For
            // example, Binance.Net, Bitfinex.Net, Kraken.Net, etc.  Many
            // libraries accept API credentials via constructor parameters.

            try
            {
                await InternalStartAsync();
                if (Status == ePluginStatus.STOPPED_FAILED)
                    return;

                // Inform VisualHFT that the provider is connected
                RaiseOnDataReceived(GetProviderModel(eSESSIONSTATUS.CONNECTED));
                Status = ePluginStatus.STARTED;
            }
            catch (Exception ex)
            {
                // If initialization fails, propagate the error to the base class
                // so that reconnection logic can kick in.
                await HandleConnectionLost(ex.Message, ex);
            }
        }

        /// <summary>
        /// Contains your actual startup logic.  Do not call this directly
        /// outside of StartAsync().  It is used by the base class to
        /// implement automatic reconnection.
        /// </summary>
        private async Task InternalStartAsync()
        {
            // Loop over all configured symbols and subscribe.  You may want
            // to normalise symbols here by calling GetNormalizedSymbol().
            foreach (string symbol in GetAllNormalizedSymbols())
            {
                // TODO: Replace this with calls to your exchange client
                // For example:
                // await client.SubscribeToOrderBookUpdatesAsync(symbol, _settings.DepthLevels, OnOrderBookUpdate);
                // await client.SubscribeToTradeUpdatesAsync(symbol, OnTradeUpdate);
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Called by the plug‑in manager when the user stops this plug‑in.
        /// Unsubscribes from all feeds and cleans up resources.
        /// </summary>
        public override async Task StopAsync()
        {
            Status = ePluginStatus.STOPPING;
            // TODO: Unsubscribe your exchange client and dispose resources here

            // Notify VisualHFT that the provider is disconnected and clear the
            // orderbook snapshot for this provider.
            RaiseOnDataReceived(new List<OrderBook>());
            RaiseOnDataReceived(GetProviderModel(eSESSIONSTATUS.DISCONNECTED));
            await base.StopAsync();
        }

        /// <summary>
        /// Handler for orderbook updates from the exchange.  Convert the raw
        /// payload into VisualHFT.Model.OrderBook and publish it via
        /// RaiseOnDataReceived().  Note that OrderBook has a Provider
        /// property which should be set to Name and a Symbol property
        /// containing the normalised symbol.
        /// </summary>
        /// <param name="update">Raw exchange payload.</param>
        private void OnOrderBookUpdate(object update)
        {
            // TODO: Parse update into a VisualHFT.Model.OrderBook instance.  At
            // minimum you should populate Bids, Asks, Symbol, Provider and
            // Timestamp.  Use GetNormalizedSymbol() to map the exchange
            // symbol into your configured symbol list.

            // Example stub (remove once implemented):
            var orderBook = new OrderBook
            {
                Provider = Name,
                // Symbol = GetNormalizedSymbol(rawSymbol),
                // Bids = new List<OrderBookLevel> { ... },
                // Asks = new List<OrderBookLevel> { ... },
                // Timestamp = DateTime.UtcNow
            };
            RaiseOnDataReceived(orderBook);
        }

        /// <summary>
        /// Handler for trade updates from the exchange.  Convert the raw
        /// payload into VisualHFT.Model.Trade and publish via
        /// RaiseOnDataReceived().
        /// </summary>
        /// <param name="update">Raw trade payload.</param>
        private void OnTradeUpdate(object update)
        {
            // TODO: Parse update into a VisualHFT.Model.Trade instance.  A trade
            // must set Symbol, Price, Size, IsBuyerMaker and Timestamp.

            // Example stub (remove once implemented):
            var trade = new Trade
            {
                Provider = Name,
                // Symbol = GetNormalizedSymbol(rawSymbol),
                // Price = rawTrade.Price,
                // Size = rawTrade.Quantity,
                // IsBuyerMaker = rawTrade.IsBuyerMaker,
                // Timestamp = DateTime.UtcNow
            };
            RaiseOnDataReceived(trade);
        }
    }
}