using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using VisualHFT.Model;

namespace VisualHFT.ViewModel
{
    public class PositionViewModel : BindableBase
    {
        private readonly Position _position;
        private ObservableCollection<VisualHFT.ViewModel.Model.Order> _orders;
        private readonly ReaderWriterLockSlim _positionLock = new ReaderWriterLockSlim();
        private readonly ReaderWriterLockSlim _ordersLock = new ReaderWriterLockSlim();

        public PositionViewModel(Position position)
        {
            _position = position;
            _orders = new ObservableCollection<VisualHFT.ViewModel.Model.Order>();
            
            foreach (var o in position.GetAllOrders(null))
            {
                _orders.Add(new VisualHFT.ViewModel.Model.Order(o));
            }
        }
        public string Symbol
        {
            get
            {
                _positionLock.EnterReadLock();
                try
                {
                    return _position.Symbol;
                }
                finally
                {
                    _positionLock.ExitReadLock();
                }
            }
        }

        public double TotBuy
        {
            get
            {
                _positionLock.EnterReadLock();
                try
                {
                    return _position.TotBuy;
                }
                finally
                {
                    _positionLock.ExitReadLock();
                }
            }
        }

        public double TotSell
        {
            get
            {
                _positionLock.EnterReadLock();
                try
                {
                    return _position.TotSell;
                }
                finally
                {
                    _positionLock.ExitReadLock();
                }
            }
        }

        public double WrkBuy
        {
            get
            {
                _positionLock.EnterReadLock();
                try
                {
                    return _position.WrkBuy;
                }
                finally
                {
                    _positionLock.ExitReadLock();
                }
            }
        }

        public double WrkSell
        {
            get
            {
                _positionLock.EnterReadLock();
                try
                {
                    return _position.WrkSell;
                }
                finally
                {
                    _positionLock.ExitReadLock();
                }
            }
        }

        public double PLTot
        {
            get
            {
                _positionLock.EnterReadLock();
                try
                {
                    return _position.PLTot;
                }
                finally
                {
                    _positionLock.ExitReadLock();
                }
            }
        }

        public double PLRealized
        {
            get
            {
                _positionLock.EnterReadLock();
                try
                {
                    return _position.PLRealized;
                }
                finally
                {
                    _positionLock.ExitReadLock();
                }
            }
        }

        public double PLOpen
        {
            get
            {
                _positionLock.EnterReadLock();
                try
                {
                    return _position.PLOpen;
                }
                finally
                {
                    _positionLock.ExitReadLock();
                }
            }
        }

        public DateTime LastUpdated
        {
            get
            {
                _positionLock.EnterReadLock();
                try
                {
                    return _position.LastUpdated;
                }
                finally
                {
                    _positionLock.ExitReadLock();
                }
            }
        }

        public double NetPosition
        {
            get
            {
                _positionLock.EnterReadLock();
                try
                {
                    return _position.NetPosition;
                }
                finally
                {
                    _positionLock.ExitReadLock();
                }
            }
        }

        public double Exposure
        {
            get
            {
                _positionLock.EnterReadLock();
                try
                {
                    return _position.Exposure;
                }
                finally
                {
                    _positionLock.ExitReadLock();
                }
            }
        }

        public void UpdateCurrentMidPrice(double value)
        {
            // Throttle updates to avoid processing all calls
            if (DateTime.UtcNow.Subtract(_position.LastUpdated).TotalMilliseconds < 1000)
                return;

            bool needToChange = false;
            _positionLock.EnterWriteLock();
            try
            {
                needToChange = _position.UpdateCurrentMidPrice(value);
            }
            finally
            {
                _positionLock.ExitWriteLock();
            }
            if (needToChange) //call this outside the lock
                UpdateUI();
        }

        public ObservableCollection<VisualHFT.ViewModel.Model.Order> Orders
        {
            get
            {
                _ordersLock.EnterReadLock();
                try
                {
                    return _orders;
                }
                finally
                {
                    _ordersLock.ExitReadLock();
                }
            }
        }


        public void AddOrder(Order addedOrder)
        {
            _ordersLock.EnterWriteLock();
            try
            {
                _orders.Add(new Model.Order(addedOrder));
            }
            finally
            {
                _ordersLock.ExitWriteLock();
            }
        }

        public void UpdateOrder(Order updatedOrder)
        {
            _ordersLock.EnterWriteLock();
            try
            {
                var existingOrder = _orders.FirstOrDefault(x => x.OrderID == updatedOrder.OrderID);
                existingOrder?.Update(updatedOrder);
            }
            finally
            {
                _ordersLock.ExitWriteLock();
            }
        }
        public void UpdateUI()
        {
            RaisePropertyChanged(nameof(TotBuy));
            RaisePropertyChanged(nameof(TotSell));
            RaisePropertyChanged(nameof(WrkBuy));
            RaisePropertyChanged(nameof(WrkSell));
            RaisePropertyChanged(nameof(PLTot));
            RaisePropertyChanged(nameof(PLRealized));
            RaisePropertyChanged(nameof(PLOpen));
            RaisePropertyChanged(nameof(LastUpdated));
            RaisePropertyChanged(nameof(NetPosition));
            RaisePropertyChanged(nameof(Exposure));
            RaisePropertyChanged(nameof(Orders));
        }


    }
}
