using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using VisualHFT.Helpers;
using VisualHFT.Model;
using Prism.Mvvm;
using System.Windows.Input;
using System.Windows.Data;
using VisualHFT.Commons.Helpers;
using System.Threading.Tasks;
using VisualHFT.Simulators;

namespace VisualHFT.ViewModel
{
    public class vmPosition : BindableBase, IDisposable
    {
        private string _selectedSymbol;
        private DateTime _selectedDate;
        private Dictionary<string, Func<string, string, bool>> _dialogs;
        private ObservableCollection<VisualHFT.ViewModel.Model.Order> _allOrders;
        private ObservableCollection<PositionViewModel> _positions;
        // Dictionary to quickly lookup PositionViewModel by Position identifier (e.g., PositionId)
        private readonly Dictionary<string, PositionViewModel> _positionViewModelLookup = new Dictionary<string, PositionViewModel>();

        private readonly ReaderWriterLockSlim _lockOrders = new ReaderWriterLockSlim();
        private readonly ReaderWriterLockSlim _lockPosMgr = new ReaderWriterLockSlim();
        private bool _disposed = false; // to track whether the object has been disposed
        private string _selectedFilter = "Working";
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public ICollectionView OrdersView { get; }
        public ICommand FilterCommand { get; }

        public bool IsAllFilterSelected => _selectedFilter == "All";
        public ObservableCollection<VisualHFT.ViewModel.Model.Order> AllOrders
        {
            get
            {
                _lockOrders.EnterReadLock();
                try
                {
                    return _allOrders;
                }
                finally
                {
                    _lockOrders.ExitReadLock();
                }
            }
        }
        public ObservableCollection<PositionViewModel> Positions
        {
            get
            {
                _lockPosMgr.EnterReadLock();
                try
                {
                    return _positions;
                }
                finally
                {
                    _lockPosMgr.ExitReadLock();
                }
            }
        }
        public string SelectedSymbol
        {
            get => _selectedSymbol;
            set => SetProperty(ref _selectedSymbol, value, onChanged: () => ReloadOrders());
        }
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set => SetProperty(ref _selectedDate, value, onChanged: () => ReloadOrders());
        }
        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                SetProperty(ref _selectedFilter, value);
                ApplyFilter();
            }
        }

        public vmPosition(Dictionary<string, Func<string, string, bool>> dialogs)
        {
            this._dialogs = dialogs;
            _positions = new ObservableCollection<PositionViewModel>();
            FilterCommand = new RelayCommand<string>(OnFilterChanged);
            this.SelectedDate = HelperTimeProvider.Now; //new DateTime(2022, 10, 6); 
            _lockOrders.EnterWriteLock();
            try
            {
                _allOrders = new ObservableCollection<VisualHFT.ViewModel.Model.Order>();
                OrdersView = CollectionViewSource.GetDefaultView(_allOrders);
                OrdersView.SortDescriptions.Add(new SortDescription("CreationTimeStamp", ListSortDirection.Descending));
                SelectedFilter = "Working";
                ApplyFilter();
            }
            finally
            {
                _lockOrders.ExitWriteLock();
            }
            _positions.CollectionChanged += OnPositionsCollectionChanged;
            RaisePropertyChanged(nameof(Positions));

            HelperOrderBook.Instance.Subscribe(LIMITORDERBOOK_OnDataReceived);
            HelperPosition.Instance.Subscribe(POSITIONS_OnDataUpdated);

            HelperTimeProvider.OnSetFixedTime += HelperTimeProvider_OnSetFixedTime;




            //start Order execution simulation
            //Task.Run(async () => await ExchangeExecutionSimulator.Instance.StartSimulationAsync());




        }

        ~vmPosition()
        {
            Dispose(false);
        }

        private void OnPositionsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                if (e.NewItems != null)
                {
                    foreach (PositionViewModel newPosition in e.NewItems.OfType<PositionViewModel>())
                    {
                        //add orders to the newly position added
                        foreach (var order in newPosition.Orders)
                        {
                            _lockOrders.EnterWriteLock();
                            try
                            {
                                _allOrders.Add(order);
                            }
                            finally
                            {
                                _lockOrders.ExitWriteLock();
                            }
                        }
                        newPosition.Orders.CollectionChanged += OnPositionOrders_CollectionChanged;
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                if (e.OldItems != null)
                {
                    foreach (PositionViewModel oldPosition in e.OldItems.OfType<PositionViewModel>())
                    {
                        //remove orders from the position removed
                        foreach (var order in oldPosition.Orders)
                        {
                            _lockOrders.EnterWriteLock();
                            try
                            {
                                _allOrders.Remove(order);
                            }
                            finally
                            {
                                _lockOrders.ExitWriteLock();
                            }
                        }
                        oldPosition.Orders.CollectionChanged -= OnPositionOrders_CollectionChanged;
                    }
                }
            }
        }

        private void OnPositionOrders_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is ObservableCollection<VisualHFT.ViewModel.Model.Order> orders)
            {
                _lockOrders.EnterWriteLock();
                try
                {
                    if (e.Action == NotifyCollectionChangedAction.Add)
                    {
                        foreach (var order in e.NewItems.OfType<VisualHFT.ViewModel.Model.Order>())
                        {
                            _allOrders.Add(order);
                        }
                    }
                    else if (e.Action == NotifyCollectionChangedAction.Remove)
                    {
                        foreach (var order in e.OldItems.OfType<VisualHFT.ViewModel.Model.Order>())
                        {
                            _allOrders.Remove(order);
                        }
                    }
                }
                finally
                {
                    _lockOrders.ExitWriteLock();
                }
            }
        }

        private void OnFilterChanged(string filter)
        {
            SelectedFilter = filter;
        }

        private void POSITIONS_OnDataUpdated(Position position, Order? addedOrder, Order? updatedOrder)
        {
            App.Current.Dispatcher.BeginInvoke(() => ProcessPositionUpdate(position, addedOrder, updatedOrder));
        }

        private void LIMITORDERBOOK_OnDataReceived(OrderBook e)
        {
            _lockPosMgr.EnterWriteLock();
            try
            {
                if (_positionViewModelLookup != null && _positionViewModelLookup.TryGetValue(e.Symbol, out var positionViewModel))
                {
                    positionViewModel.UpdateCurrentMidPrice(e.MidPrice);
                }
            }
            finally
            {
                _lockPosMgr.ExitWriteLock();
            }
        }

        private void ProcessPositionUpdate(Position updatedPosition, Order? addedOrder, Order? updatedOrder)
        {
            if (updatedPosition == null)
                return;
            PositionViewModel positionViewModel;

            // Check if a PositionViewModel already exists for this incoming Position update
            _lockPosMgr.EnterWriteLock();
            try
            {
                if (!_positionViewModelLookup.TryGetValue(updatedPosition.Symbol, out positionViewModel))
                {
                    //New position
                    positionViewModel = new PositionViewModel(updatedPosition);
                    _positions.Add(positionViewModel); //this will trigger OnPositionsCollectionChanged
                    _positionViewModelLookup.Add(updatedPosition.Symbol, positionViewModel);
                }
                else
                {
                    if (addedOrder != null)
                        positionViewModel.AddOrder(addedOrder);
                    if (updatedOrder != null)
                        positionViewModel.UpdateOrder(updatedOrder);
                }
            }
            finally
            {
                _lockPosMgr.ExitWriteLock();
            }
            OrdersView.Refresh();
        }

        private void ReloadOrders()
        {
            var allPositions = HelperPosition.Instance.GetAllPositions();
            App.Current.Dispatcher.BeginInvoke(() =>
            {
                _lockPosMgr.EnterWriteLock();
                try
                {
                    _positions.Clear();
                    _positionViewModelLookup.Clear();
                    foreach (var pos in allPositions)
                    {
                        var positionViewModel = new PositionViewModel(pos);
                        _positions.Add(positionViewModel); //this will trigger OnPositionsCollectionChanged
                        _positionViewModelLookup.Add(pos.Symbol, positionViewModel);
                    }

                    SelectedFilter = "Working";
                }
                finally
                {
                    _lockPosMgr.ExitWriteLock();
                }
            });

            RaisePropertyChanged(nameof(AllOrders));
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrEmpty(SelectedFilter))
                return;
            switch (SelectedFilter)
            {
                case "Working":
                    OrdersView.Filter = o =>
                    {
                        var order = (VisualHFT.Model.Order)o;
                        return new[] { "NONE", "SENT", "NEW", "PARTIALFILLED", "CANCELEDSENT", "REPLACESENT", "REPLACED" }.Contains(order.Status.ToString())
                               && order.CreationTimeStamp.Date == _selectedDate.Date;
                    };
                    break;
                case "Filled":
                    OrdersView.Filter = o =>
                    {
                        var order = (VisualHFT.Model.Order)o;
                        return (order.Status.ToString() == "FILLED" || order.Status.ToString() == "PARTIALFILLED")
                               && order.CreationTimeStamp.Date == _selectedDate.Date;
                    };
                    break;
                case "Cancelled":
                    OrdersView.Filter = o =>
                    {
                        var order = (VisualHFT.Model.Order)o;
                        return new[] { "CANCELED", "REJECTED" }.Contains(order.Status.ToString())
                               && order.CreationTimeStamp.Date == _selectedDate.Date;
                    };
                    break;
                case "All":
                    OrdersView.Filter = o => ((VisualHFT.Model.Order)o).CreationTimeStamp.Date == _selectedDate.Date;
                    //OrdersView.Filter = null;
                    break;
            }
            // Ensure the sort description is still in place
            if (!OrdersView.SortDescriptions.Any(sd => sd.PropertyName == "CreationTimeStamp"))
            {
                OrdersView.SortDescriptions.Add(new SortDescription("CreationTimeStamp", ListSortDirection.Descending));
            }

            OrdersView.Refresh();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    HelperOrderBook.Instance.Unsubscribe(LIMITORDERBOOK_OnDataReceived);
                    HelperPosition.Instance.Unsubscribe(POSITIONS_OnDataUpdated);
                    HelperTimeProvider.OnSetFixedTime -= HelperTimeProvider_OnSetFixedTime;
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void HelperTimeProvider_OnSetFixedTime(object? sender, EventArgs e)
        {
            if (_selectedDate != HelperTimeProvider.Now.Date)
                SelectedDate = HelperTimeProvider.Now.Date;
        }
    }
}
