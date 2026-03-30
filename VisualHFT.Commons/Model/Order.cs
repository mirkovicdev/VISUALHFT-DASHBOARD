using VisualHFT.Enums;

using VisualHFT.Commons.Model;

namespace VisualHFT.Model
{
    public partial class Order : IResettable
    {
        /// <summary>
        /// This override will fire PostedSecondsAgo property change when any other property fires
        /// </summary>
        /// <param name="args"></param>

        #region private fields
        private string _providerName;
        private long _orderID;
        private string _strategyCode;
        private string _symbol;
        private int _providerId;
        private string _clOrdId;
        private eORDERSIDE _side;
        private eORDERTYPE _orderType;
        private eORDERTIMEINFORCE _timeInForce;
        private eORDERSTATUS _status;
        private double _quantity;
        private double _minQuantity;
        private double _filledQuantity;
        private double _pricePlaced;
        private string _currency;
        private string _futSettDate;
        private bool _isMM;
        private bool _isEmpty;
        private string _layerName;
        private int _attemptsToClose;
        private int _symbolMultiplier;
        private int _symbolDecimals;
        private string _freeText;
        private string _originPartyID;
        private List<Execution> _executions;
        private int _quoteID;
        private DateTime _quoteServerTimeStamp;
        private DateTime _quoteLocalTimeStamp;
        private DateTime _creationTimeStamp;
        private DateTime _lastUpdate;
        private DateTime _executedTimeStamp;
        private DateTime _fireSignalTimestamp;
        private double _stopLoss;
        private double _takeProfit;
        private bool _pipsTrail;
        private double _unrealizedPnL;
        private double _maxDrowdown;
        private double _bestBid;
        private double _bestAsk;
        private double _getAvgPrice;
        private double _getQuantity;
        private double _filledPercentage;
        #endregion

        public Order()
        {
            IsEmpty = true;
        }
        public Order(Order order)
        {
            Update(order);
        }
        public void Update(Order order)
        {
            ProviderName = order.ProviderName;
            OrderID = order.OrderID;
            Symbol = order.Symbol;
            ProviderId = order.ProviderId;
            ClOrdId = order.ClOrdId;
            Side = order.Side;
            OrderType = order.OrderType;
            TimeInForce = order.TimeInForce;
            Status = order.Status;
            Quantity = order.Quantity;
            FilledQuantity = order.FilledQuantity;
            PricePlaced = order.PricePlaced;
            Currency = order.Currency;
            IsEmpty = order.IsEmpty;
            FreeText = order.FreeText;
            Executions = order.Executions;
            CreationTimeStamp = order.CreationTimeStamp;
            BestAsk = order.BestAsk;
            BestBid = order.BestBid;
            FilledPrice = order.FilledPrice;

            LastUpdated = HelperTimeProvider.Now;
        }

        public void Reset()
        {
            ProviderName = "";
            OrderID = 0;
            Symbol = "";
            ProviderId = 0;
            ClOrdId = "";
            Side = eORDERSIDE.None;
            OrderType = eORDERTYPE.NONE;
            TimeInForce = eORDERTIMEINFORCE.NONE;
            Status = eORDERSTATUS.NONE;
            Quantity = 0;
            FilledQuantity = 0;
            FilledPrice = 0;
            PricePlaced = 0;
            Currency = "";
            IsEmpty = true;
            FreeText = "";
            if (Executions != null)
                Executions.Clear();
            else
                Executions = new List<Execution>();
            CreationTimeStamp = DateTime.MinValue;
            BestAsk = 0;
            BestBid = 0;
            LastUpdated = HelperTimeProvider.Now;
        }

        public double PendingQuantity => (Status != eORDERSTATUS.CANCELED && Status != eORDERSTATUS.REJECTED ? Quantity - FilledQuantity: 0);
        public string ProviderName
        {
            get => _providerName;
            set => _providerName = value;
        }
        public long OrderID
        {
            get => _orderID;
            set => _orderID = value;
        }
        public string Symbol
        {
            get => _symbol;
            set => _symbol = value;
        }
        public int ProviderId
        {
            get => _providerId;
            set => _providerId = value;
        }
        public string ClOrdId
        {
            get => _clOrdId;
            set => _clOrdId = value;
        }
        public eORDERSIDE Side
        {
            get => _side;
            set => _side = value;
        }
        public eORDERTYPE OrderType
        {
            get => _orderType;
            set => _orderType = value;
        }
        public eORDERTIMEINFORCE TimeInForce
        {
            get => _timeInForce;
            set => _timeInForce = value;
        }
        public eORDERSTATUS Status
        {
            get => _status;
            set => _status = value;
        }
        public double Quantity
        {
            get => _quantity;
            set => _quantity = value;
        }
        public double FilledQuantity
        {
            get => _filledQuantity;
            set => _filledQuantity = value;
        }
        public double PricePlaced
        {
            get => _pricePlaced;
            set => _pricePlaced = value;
        }
        public string Currency
        {
            get => _currency;
            set => _currency = value;
        }
        public bool IsEmpty
        {
            get => _isEmpty;
            set => _isEmpty = value;
        }
        public string FreeText
        {
            get => _freeText;
            set => _freeText = value;
        }
        public List<Execution> Executions
        {
            get => _executions;
            set => _executions = value;
        }
        public DateTime CreationTimeStamp
        {
            get => _creationTimeStamp;
            set => _creationTimeStamp = value;
        }
        public DateTime LastUpdated
        {
            get => _lastUpdate;
            set => _lastUpdate = value;
        }
        public double BestBid
        {
            get => _bestBid;
            set => _bestBid = value;
        }
        public double BestAsk
        {
            get => _bestAsk;
            set => _bestAsk = value;
        }
        public double FilledPercentage
        {
            get => _filledPercentage;
            set => _filledPercentage = value;
        }
        public double FilledPrice { get; set; }
    }

}
