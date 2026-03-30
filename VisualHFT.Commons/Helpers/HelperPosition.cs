using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisualHFT.DataTradeRetriever;
using VisualHFT.Enums;
using VisualHFT.Helpers;
using VisualHFT.Model;
using ErrorEventArgs = VisualHFT.Commons.Model.ErrorEventArgs;

namespace VisualHFT.Commons.Helpers
{
    public class HelperPosition
    {
        private Dictionary<string, VisualHFT.Model.Position> _positionsBySymbol;
        private List<Action<Position, Order?, Order?>> _subscribers = new List<Action<Position, Order?, Order?>>();

        private readonly ReaderWriterLockSlim _lockObj = new ReaderWriterLockSlim();
        private readonly ReaderWriterLockSlim _lockPos = new ReaderWriterLockSlim();

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly HelperPosition instance = new HelperPosition();
        public event Action<VisualHFT.Commons.Model.ErrorEventArgs> OnException;
        public static HelperPosition Instance => instance;

        public HelperPosition()
        {
            _positionsBySymbol = new Dictionary<string, Position>();
        }

        public void Subscribe(Action<Position, Order?, Order?> subscriber)
        {
            _lockObj.EnterWriteLock();
            try
            {
                _subscribers.Add(subscriber);
            }
            finally
            {
                _lockObj.ExitWriteLock();
            }
        }

        public void Unsubscribe(Action<Position, Order?, Order?> subscriber)
        {
            _lockObj.EnterWriteLock();
            try
            {
                _subscribers.Remove(subscriber);
            }
            finally
            {
                _lockObj.ExitWriteLock();
            }
        }

        public void UpdateData(VisualHFT.Model.Order data)
        {
            AddExecutedOrderInternal(data);
        }

        public void UpdateData(IEnumerable<VisualHFT.Model.Order> data)
        {
            foreach (var e in data)
            {
                UpdateData(e);
            }
        }

        public List<VisualHFT.Model.Position> GetAllPositions()
        {
            _lockPos.EnterReadLock();
            try
            {
                return _positionsBySymbol.Values.ToList();
            }
            finally
            {
                _lockPos.ExitReadLock();
            }
        }

        private void AddExecutedOrderInternal(Order? executedOrder)
        {
            if (executedOrder == null)
                return;
            if (string.IsNullOrEmpty(executedOrder.Symbol))
                throw new ArgumentException("Symbol cannot be null or empty.");

            _lockPos.EnterWriteLock();
            try
            {
                if (!_positionsBySymbol.ContainsKey(executedOrder.Symbol))
                {
                    _positionsBySymbol.Add(executedOrder.Symbol, new Position(executedOrder.Symbol, PositionManagerCalculationMethod.FIFO));
                }
                Order? addedOrder = null;
                Order? updatedOrder = null;
                _positionsBySymbol[executedOrder.Symbol].AddOrUpdateOrder(executedOrder, out addedOrder, out updatedOrder);
                DispatchToSubscribers(_positionsBySymbol[executedOrder.Symbol], addedOrder, updatedOrder);


            }
            finally
            {
                _lockPos.ExitWriteLock();
            }
        }

        private void DispatchToSubscribers(Position position, Order? addedOrder, Order? updatedOrder)
        {
            _lockObj.EnterReadLock();
            try
            {
                foreach (var subscriber in _subscribers)
                {
                    try
                    {
                        subscriber(position, addedOrder, updatedOrder);
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
            finally
            {
                _lockObj.ExitReadLock();
            }
        }

        public void Reset()
        {
            //unsubscribe all
            foreach (var subscriber in _subscribers)
            {
                Unsubscribe(subscriber);
            }
            _positionsBySymbol.Clear();
        }
    }
}
