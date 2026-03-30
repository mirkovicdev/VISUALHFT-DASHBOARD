using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using VisualHFT.Commons.PluginManager;
using VisualHFT.Enums;
using VisualHFT.Helpers;
using VisualHFT.Model;
using VisualHFT.PluginManager;
using VisualHFT.Studies.VPIN.Model;
using VisualHFT.Studies.VPIN.UserControls;
using VisualHFT.Studies.VPIN.ViewModel;
using VisualHFT.UserSettings;

namespace VisualHFT.Studies
{
    /// <summary>
    /// VPIN (Volume-Synchronized Probability of Informed Trading) measures order flow toxicity
    /// using volume-synchronized buckets per Easley, Lopez de Prado & O'Hara (2012).
    ///
    /// Formula: VPIN = (1/n) * SUM |V_buy_i - V_sell_i| / V_bucket, over n completed buckets.
    ///
    /// Range [0, 1]: 0 = balanced flow, 1 = fully toxic (all buys or all sells).
    /// </summary>
    public class VPINStudy : BasePluginStudy
    {
        private const string ValueFormat = "N2";
        private const string colorGreen = "Green";
        private const string colorWhite = "White";
        private const int DEFAULT_NUMBER_OF_BUCKETS = 50;

        private bool _disposed = false; // to track whether the object has been disposed
        private PlugInSettings _settings;
        private readonly object _lockBucket = new object();

        //variables for calculation
        private decimal _bucketVolumeSize; // The volume size of each bucket
        private decimal _currentBucketVolume; // Running accumulated volume in current bucket
        private decimal _lastMarketMidPrice = 0; //keep track of market price
        private decimal _currentBuyVolume = 0;
        private decimal _currentSellVolume = 0;

        // Rolling window of completed bucket imbalances: |V_buy - V_sell| / V_bucket
        private decimal[] _bucketImbalances;
        private int _bufferIndex = 0;
        private int _bufferCount = 0;
        private decimal _rollingSum = 0; // Running sum for O(1) average calculation


        // Event declaration
        public override event EventHandler<decimal> OnAlertTriggered;

        public override string Name { get; set; } = "VPIN Study Plugin";
        public override string Version { get; set; } = "1.0.0";
        public override string Description { get; set; } = "Volume-Synchronized Probability of Informed Trading (VPIN) measures buy/sell volume imbalance in fixed buckets. Provides real-time risk assessment (0-1 scale) for market instability detection.";
        public override string Author { get; set; } = "VisualHFT";
        public override ISetting Settings { get => _settings; set => _settings = (PlugInSettings)value; }
        public override Action CloseSettingWindow { get; set; }
        public override string TileTitle { get; set; } = "VPIN";
        public override string TileToolTip { get; set; } = "<b>Volume-Synchronized Probability of Informed Trading</b> (VPIN) is a real-time metric that measures the imbalance between buy and sell volumes, reflecting potential market risk or instability. <br/>VPIN is crucial for traders and analysts to gauge market sentiment and anticipate liquidity and volatility shifts.<br/><br/>" +
                "VPIN is calculated through the accumulation of trade volumes into fixed-size buckets. Each bucket captures a snapshot of trading activity, enabling ongoing analysis of market dynamics:<br/>" +
                "1. <b>Trade Classification:</b> Trades are categorized as buys or sells based on their relation to the market mid-price at execution.<br/>" +
                "2. <b>Volume Accumulation:</b> Buy and sell volumes are accumulated separately until reaching a pre-set bucket size.<br/>" +
                "3. <b>VPIN Calculation:</b> VPIN is the absolute difference between buy and sell volumes in a bucket, normalized to total volume, ranging from 0 (balanced trading) to 1 (high imbalance).<br/><br/>" +
                "To enhance real-time relevance, VPIN values are updated with 'Interim Updates' during the filling of each bucket, providing a more current view of market conditions. These updates offer a dynamic and timely insight into market liquidity and informed trading activity. VPIN serves as an early warning indicator of market turbulence, particularly valuable in high-frequency trading environments.";

        public decimal BucketVolumeSize => _bucketVolumeSize;

        public VPINStudy()
        {
            _bucketImbalances = new decimal[DEFAULT_NUMBER_OF_BUCKETS];
        }
        ~VPINStudy()
        {
            Dispose(false);
        }

        public override async Task StartAsync()
        {
            await base.StartAsync();//call the base first
            ResetBucket();

            HelperOrderBook.Instance.Subscribe(LIMITORDERBOOK_OnDataReceived);
            HelperTrade.Instance.Subscribe(TRADES_OnDataReceived);
            DoCalculation(false); //initial value

            log.Info($"{this.Name} Plugin has successfully started.");
            Status = ePluginStatus.STARTED;
        }

        public override async Task StopAsync()
        {
            Status = ePluginStatus.STOPPING;
            log.Info($"{this.Name} is stopping.");

            HelperOrderBook.Instance.Unsubscribe(LIMITORDERBOOK_OnDataReceived);
            HelperTrade.Instance.Unsubscribe(TRADES_OnDataReceived);

            await base.StopAsync();
        }


        private void TRADES_OnDataReceived(Trade e)
        {
            /*
             * ***************************************************************************************************
             * TRANSFORM the incoming object (decouple it)
             * DO NOT hold this call back, since other components depends on the speed of this specific call back.
             * DO NOT BLOCK
             * IDEALLY, USE QUEUES TO DECOUPLE
             * ***************************************************************************************************
             */

            if (e == null)
                return;
            if (_settings.Provider.ProviderID != e.ProviderId || _settings.Symbol != e.Symbol)
                return;

            lock (_lockBucket)
            {
                if (_bucketVolumeSize == 0)
                    _bucketVolumeSize = (decimal)_settings.BucketVolSize;

                // Tick rule: classify using mid-price from the order book
                // Price >= mid → buy (aggressor lifting the ask)
                // Price <  mid → sell (aggressor hitting the bid)
                // Fallback to provider's IsBuy if no mid-price yet
                bool isBuy;
                if (_lastMarketMidPrice > 0)
                    isBuy = e.Price >= _lastMarketMidPrice;
                else if (e.IsBuy.HasValue)
                    isBuy = e.IsBuy.Value;
                else
                    return; // No classification possible

                decimal remainingSize = e.Size;

                // Assign entire trade to buy or sell for the current bucket portion
                if (isBuy)
                    _currentBuyVolume += remainingSize;
                else
                    _currentSellVolume += remainingSize;
                _currentBucketVolume += remainingSize;

                // Complete as many buckets as this trade fills
                while (_currentBucketVolume >= _bucketVolumeSize && _bucketVolumeSize > 0)
                {
                    decimal bucketOverflow = _currentBucketVolume - _bucketVolumeSize;

                    // Trim the overflow from whichever side received it
                    if (isBuy)
                        _currentBuyVolume -= bucketOverflow;
                    else
                        _currentSellVolume -= bucketOverflow;
                    _currentBucketVolume = _bucketVolumeSize;

                    DoCalculation(true); // Bucket completed

                    // Start new bucket with the overflow
                    _currentBuyVolume = 0;
                    _currentSellVolume = 0;
                    if (isBuy)
                        _currentBuyVolume = bucketOverflow;
                    else
                        _currentSellVolume = bucketOverflow;
                    _currentBucketVolume = bucketOverflow;
                }

                DoCalculation(false); // Interim update with current state
            }
        }
        private void LIMITORDERBOOK_OnDataReceived(OrderBook e)
        {
            /*
             * ***************************************************************************************************
             * TRANSFORM the incoming object (decouple it)
             * DO NOT hold this call back, since other components depends on the speed of this specific call back.
             * DO NOT BLOCK
             * IDEALLY, USE QUEUES TO DECOUPLE
             * ***************************************************************************************************
             */

            if (e == null)
                return;
            if (_settings.Provider.ProviderID != e.ProviderID || _settings.Symbol != e.Symbol)
                return;

            lock (_lockBucket)
            {
                _lastMarketMidPrice = (decimal)e.MidPrice;
                DoCalculation(false); //Interim update -> Just to send update.
            }
        }
        private void DoCalculation(bool isNewBucket)
        {
            // Caller must hold _lockBucket
            if (Status != VisualHFT.PluginManager.ePluginStatus.STARTED) return;
            string valueColor = isNewBucket ? colorGreen : colorWhite;

            if (isNewBucket && _bucketVolumeSize > 0)
            {
                // Completed bucket: push imbalance into rolling window
                decimal bucketImbalance = Math.Abs(_currentBuyVolume - _currentSellVolume) / _bucketVolumeSize;

                // Subtract the value being evicted (if buffer is full)
                if (_bufferCount == _bucketImbalances.Length)
                    _rollingSum -= _bucketImbalances[_bufferIndex];
                else
                    _bufferCount++;

                _bucketImbalances[_bufferIndex] = bucketImbalance;
                _rollingSum += bucketImbalance;
                _bufferIndex = (_bufferIndex + 1) % _bucketImbalances.Length;
            }

            // VPIN = average of completed bucket imbalances in the rolling window
            decimal vpin = 0;
            if (_bufferCount > 0)
                vpin = _rollingSum / _bufferCount;

            var newItem = new BaseStudyModel();
            newItem.Value = vpin;
            newItem.Format = ValueFormat;
            newItem.Timestamp = HelperTimeProvider.Now;
            newItem.MarketMidPrice = _lastMarketMidPrice;
            newItem.ValueColor = valueColor;
            newItem.AddItemSkippingAggregation = isNewBucket;

            AddCalculation(newItem);
        }
        private void ResetBucket()
        {
            lock (_lockBucket)
            {
                _bucketVolumeSize = 0;
                _currentSellVolume = 0;
                _currentBuyVolume = 0;
                _currentBucketVolume = 0;

                int n = _settings?.NumberOfBuckets ?? DEFAULT_NUMBER_OF_BUCKETS;
                if (n <= 0) n = DEFAULT_NUMBER_OF_BUCKETS;
                _bucketImbalances = new decimal[n];
                _bufferIndex = 0;
                _bufferCount = 0;
                _rollingSum = 0;
            }
        }
        /// <summary>
        /// This method defines how the internal AggregatedCollection should aggregate incoming items.
        /// It is invoked whenever a new item is added to the collection and aggregation is required.
        /// The method takes the existing collection of items, the new incoming item, and a counter indicating
        /// how many times the last item has been aggregated. The aggregation logic should be implemented
        /// within this method to combine or process the items as needed.
        /// </summary>
        /// <param name="dataCollection">The existing internal collection of items.</param>
        /// <param name="newItem">The new incoming item to be aggregated.</param>
        /// <param name="lastItemAggregationCount">Counter indicating how many times the last item has been aggregated.</param>
        protected override void onDataAggregation(List<BaseStudyModel> dataCollection, BaseStudyModel newItem, int lastItemAggregationCount)
        {
            //Aggregation: last
            var existing = dataCollection[^1]; // Get the last item in the collection
            existing.Value = newItem.Value;
            existing.Format = newItem.Format;
            existing.MarketMidPrice = newItem.MarketMidPrice;

            base.onDataAggregation(dataCollection, newItem, lastItemAggregationCount);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    // Dispose managed resources here
                    HelperOrderBook.Instance.Unsubscribe(LIMITORDERBOOK_OnDataReceived);
                    HelperTrade.Instance.Unsubscribe(TRADES_OnDataReceived);
                    base.Dispose();
                }

            }
        }

        protected override void LoadSettings()
        {
            _settings = LoadFromUserSettings<PlugInSettings>();
            if (_settings == null)
            {
                InitializeDefaultSettings();
            }
            if (_settings.Provider == null) //To prevent back compability with older setting formats
            {
                _settings.Provider = new Provider();
            }
            if (!_settings.NumberOfBuckets.HasValue || _settings.NumberOfBuckets.Value <= 0)
            {
                _settings.NumberOfBuckets = DEFAULT_NUMBER_OF_BUCKETS;
            }
            _settings.AggregationLevel = AggregationLevel.S1; //force to 1 second
        }

        protected override void SaveSettings()
        {
            SaveToUserSettings(_settings);
        }

        protected override void InitializeDefaultSettings()
        {
            _settings = new PlugInSettings()
            {
                BucketVolSize = 1,
                NumberOfBuckets = DEFAULT_NUMBER_OF_BUCKETS,
                Symbol = "",
                Provider = new ViewModel.Model.Provider(),
                AggregationLevel = AggregationLevel.S1
            };
            SaveToUserSettings(_settings);
        }
        public override object GetUISettings()
        {
            PluginSettingsView view = new PluginSettingsView();
            PluginSettingsViewModel viewModel = new PluginSettingsViewModel(CloseSettingWindow);
            viewModel.BucketVolumeSize = _settings.BucketVolSize;
            viewModel.NumberOfBuckets = _settings.NumberOfBuckets ?? DEFAULT_NUMBER_OF_BUCKETS;
            viewModel.SelectedSymbol = _settings.Symbol;
            viewModel.SelectedProviderID = _settings.Provider.ProviderID;
            viewModel.AggregationLevelSelection = _settings.AggregationLevel;

            viewModel.UpdateSettingsFromUI = () =>
            {
                _settings.BucketVolSize = viewModel.BucketVolumeSize;
                _settings.NumberOfBuckets = viewModel.NumberOfBuckets;
                _settings.Symbol = viewModel.SelectedSymbol;
                _settings.Provider = viewModel.SelectedProvider;
                _settings.AggregationLevel = viewModel.AggregationLevelSelection;
                _bucketVolumeSize = (decimal)_settings.BucketVolSize;
                SaveSettings();

                // Reload with the new values
                Task.Run(() =>
                {
                    ResetBucket();
                });
            };
            // Display the view, perhaps in a dialog or a new window.
            view.DataContext = viewModel;
            return view;
        }

    }
}
