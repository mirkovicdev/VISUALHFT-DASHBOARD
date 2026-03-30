using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using VisualHFT.Model;


namespace VisualHFT.DataTradeRetriever
{
    public class GenericTradesRetriever : IDataTradeRetriever
    {
        private List<VisualHFT.Model.Position> _positions;
        private List<VisualHFT.Model.Order> _executedOrders;
        private DateTime? _sessionDate = null;


        private List<Action<VisualHFT.Model.Order>> _subscribers = new List<Action<Order>>();
        private readonly object _lockSubscribers = new object();
        private readonly object _lockOrders = new object();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly GenericTradesRetriever instance = new GenericTradesRetriever();
        public event Action<VisualHFT.Commons.Model.ErrorEventArgs> OnException;
        public static GenericTradesRetriever Instance => instance;


        public GenericTradesRetriever()
        {
            _positions = new List<VisualHFT.Model.Position>();
            _executedOrders = new List<VisualHFT.Model.Order>();

            HelperTimeProvider.OnSetFixedTime += HelperTimeProvider_OnSetFixedTime;
        }



        public void Subscribe(Action<VisualHFT.Model.Order> subscriber)
        {
            lock (_lockSubscribers)
            {
                _subscribers.Add(subscriber);
            }
        }
        public void Unsubscribe(Action<VisualHFT.Model.Order> subscriber)
        {
            lock (_lockSubscribers)
            {
                _subscribers.Remove(subscriber);
            }
        }
        private void DispatchToSubscribers(VisualHFT.Model.Order executedOrder)
        {
            lock (_lockSubscribers)
            {
                foreach (var subscriber in _subscribers)
                {
                    try
                    {
                        subscriber(executedOrder);
                    }
                    catch (Exception ex)
                    {
                        Task.Run(() =>
                        {
                            log.Error(ex);
                            OnException?.Invoke(new VisualHFT.Commons.Model.ErrorEventArgs(ex, subscriber.Target));
                        });
                    }
                }
            }
        }
        public void UpdateData(VisualHFT.Model.Order data)
        {
            AddExecutedOrderInternal(data);
            DispatchToSubscribers(data);
        }
        public void UpdateData(IEnumerable<VisualHFT.Model.Order> data)
        {
            foreach (var e in data)
            {
                UpdateData(e);
            }
        }

        public void SetSessionDate(DateTime? sessionDate)
        {
            if (sessionDate != _sessionDate)
            {
                _sessionDate = sessionDate;
                lock (_lockOrders)
                    _executedOrders.Clear();
                _positions.Clear();
            }
        }
        public DateTime? GetSessionDate()
        {
            return _sessionDate;
        }





        private void HelperTimeProvider_OnSetFixedTime(object? sender, EventArgs e)
        {
            if (_sessionDate != HelperTimeProvider.Now.Date)
                SetSessionDate(HelperTimeProvider.Now.Date);
        }


        public ReadOnlyCollection<VisualHFT.Model.Order> ExecutedOrders
        {
            get { lock (_lockOrders) return _executedOrders.AsReadOnly(); }
        }
        public ReadOnlyCollection<VisualHFT.Model.Position> Positions
        {
            get { return _positions.AsReadOnly(); }
        }





        private void AddExecutedOrderInternal(Order? executedOrder)
        {
            if (executedOrder == null)
                return;
            lock (_lockOrders)
            {
                var existingOrder = _executedOrders.FirstOrDefault(x => x.OrderID == executedOrder.OrderID 
                                                                        && x.ProviderId == executedOrder.ProviderId 
                                                                        && x.Symbol == executedOrder.Symbol);
                if (existingOrder == null)
                {
                    _executedOrders.Add(executedOrder);
                }
                else
                {
                    existingOrder.Update(executedOrder);
                }
            }

        }

    }
}
