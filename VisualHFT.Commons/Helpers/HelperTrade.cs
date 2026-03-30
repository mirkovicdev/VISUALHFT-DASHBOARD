using VisualHFT.Model;

namespace VisualHFT.Helpers
{
    public class HelperTrade
    {
        private List<Action<Trade>> _subscribers = new List<Action<Trade>>();
        private readonly ReaderWriterLockSlim _lockObj = new ReaderWriterLockSlim();

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly HelperTrade instance = new HelperTrade();
        public static HelperTrade Instance => instance;


        public void Subscribe(Action<Trade> processor)
        {
            _lockObj.EnterWriteLock();
            try
            {
                _subscribers.Add(processor);
            }
            finally
            {
                _lockObj.ExitWriteLock();
            }
        }

        public void Unsubscribe(Action<Trade> processor)
        {
            _lockObj.EnterWriteLock();
            try
            {
                _subscribers.Remove(processor);
            }
            finally
            {
                _lockObj.ExitWriteLock();
            }
        }

        private void DispatchToSubscribers(Trade trade)
        {
            _lockObj.EnterReadLock();
            try
            {
                foreach (var subscriber in _subscribers)
                {
                    subscriber(trade);
                }
            }
            finally
            {
                _lockObj.ExitReadLock();
            }
        }

        public void UpdateData(Trade trade)
        {
            DispatchToSubscribers(trade);
        }

        public void UpdateData(IEnumerable<Trade> trades)
        {
            foreach (var e in trades)
            {
                DispatchToSubscribers(e);
            }
        }
    }
}