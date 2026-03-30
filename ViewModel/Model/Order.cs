using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualHFT.ViewModel.Model
{
    [AddINotifyPropertyChangedInterface]
    public class Order: VisualHFT.Model.Order
    {
        //create a constructor that takes a VisualHFT.Model.Order and copies the properties to this class
        public Order(VisualHFT.Model.Order order)
        {
            //copy all properties of o into this
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
            LastUpdated = HelperTimeProvider.Now;
        }


    }
}
