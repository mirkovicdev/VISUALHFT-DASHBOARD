using VisualHFT.Helpers;
using VisualHFT.Enums;

namespace VisualHFT.Model
{
    public class Position
    {
        private string _symbol;
        private double _totBuy;
        private double _totSell;
        private double _wrkBuy;
        private double _wrkSell;
        private double _plTot;
        private double _plRealized;
        private double _plOpen;
        private double _currentMidPrice;
        private PositionManagerCalculationMethod _method;
        private List<Order> _buys;
        private List<Order> _sells;
        private DateTime _lastUpdated;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();


        public Position(string symbol, PositionManagerCalculationMethod method)
        {
            _method = method;
            _symbol = symbol;
            _buys = new List<Order>();
            _sells = new List<Order>();
        }

        public string Symbol
        {
            get => _symbol;
            private set => _symbol = value;
        }

        public double TotBuy
        {
            get => _totBuy;
            private set => _totBuy = value;
        }

        public double TotSell
        {
            get => _totSell;
            private set => _totSell = value;
        }

        public double WrkBuy
        {
            get => _wrkBuy;
            private set => _wrkBuy = value;
        }

        public double WrkSell
        {
            get => _wrkSell;
            private set => _wrkSell = value;
        }

        public double PLTot
        {
            get => _plTot;
            private set => _plTot = value;
        }

        public double PLRealized
        {
            get => _plRealized;
            private set => _plRealized = value;
        }

        public double PLOpen
        {
            get => _plOpen;
            private set => _plOpen = value;
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            private set => _lastUpdated = value;
        }

        public double NetPosition => _totBuy - _totSell;
        public double Exposure => NetPosition * _currentMidPrice;

        public bool UpdateCurrentMidPrice(double value)
        {
            _lock.EnterReadLock();
            try
            {
                if (_currentMidPrice == value)
                    return false;
                _currentMidPrice = value;
                Recalculate(false);
                return true;
            }
            finally
            {
                _lock.ExitReadLock();
            }

        }

        public void AddOrUpdateOrder(Order newExecutionOrder, out Order? outAddedOrder, out Order? outUpdatedOrder)
        {
            if (newExecutionOrder.OrderID == 0)
                throw new ArgumentException("OrderID must be set for the execution order.");
            if (newExecutionOrder.Status == eORDERSTATUS.NONE)
                throw new ArgumentException("Status must be set for the execution order.");
            if (newExecutionOrder.Symbol != _symbol)
                throw new ArgumentException("The order's symbol does not belongs to this position.");
            bool isNewOrder = false;
            _lock.EnterWriteLock();
            try
            {
                if (newExecutionOrder.Side == eORDERSIDE.Buy)
                {
                    if (_buys == null)
                        _buys = new List<Order>();
                    var existingOrder = _buys.FirstOrDefault(x => x.OrderID == newExecutionOrder.OrderID);
                    if (existingOrder == null)
                    {
                        _buys.Add(newExecutionOrder);
                        isNewOrder = true;
                    }
                    else
                    {
                        existingOrder.Update(newExecutionOrder);
                    }
                }
                else
                {
                    if (_sells == null)
                        _sells = new List<Order>();
                    var existingOrder = _sells.FirstOrDefault(x => x.OrderID == newExecutionOrder.OrderID);
                    if (existingOrder == null)
                    {
                        _sells.Add(newExecutionOrder);
                        isNewOrder = true;
                    }
                    else
                    {
                        existingOrder.Update(newExecutionOrder);
                    }
                }
                Recalculate();
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            if (isNewOrder)
            {
                outAddedOrder = newExecutionOrder;
                outUpdatedOrder = null;
            }
            else
            {
                outAddedOrder = null;
                outUpdatedOrder = newExecutionOrder;
            }
        }

        public List<Order> GetAllOrders(DateTime? sessionDate)
        {
            _lock.EnterReadLock();
            try
            {
                if (sessionDate == null)
                    return _buys.Concat(_sells).ToList();

                return _buys.Concat(_sells)
                    .Where(order => order.CreationTimeStamp.Date == sessionDate.Value.Date)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        private void Recalculate(bool recalculateBuysSellsAgain = true)
        {
            try
            {
                if (recalculateBuysSellsAgain)
                {
                    _totBuy = _buys.Sum(x => x.FilledQuantity);
                    _totSell = _sells.Sum(x => x.FilledQuantity);

                    _wrkBuy = _buys.Sum(x => x.PendingQuantity);
                    _wrkSell = _sells.Sum(x => x.PendingQuantity);
                }

                _plRealized = HelperPnLCalculator.CalculateRealizedPnL(_buys, _sells, _method);
                _plOpen = HelperPnLCalculator.CalculateOpenPnL(_buys, _sells, _method, _currentMidPrice);
                _plTot = _plRealized + _plOpen;

                _lastUpdated = HelperTimeProvider.Now;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }


        }

    }
}
