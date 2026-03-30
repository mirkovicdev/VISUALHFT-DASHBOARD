using System.Collections.ObjectModel;
using VisualHFT.Model;

namespace VisualHFT.DataTradeRetriever
{
    public interface IDataTradeRetriever
    {
        void Subscribe(Action<VisualHFT.Model.Order> subscriber);
        void Unsubscribe(Action<VisualHFT.Model.Order> subscriber);
        void UpdateData(VisualHFT.Model.Order data);
        void UpdateData(IEnumerable<VisualHFT.Model.Order> data);


        ReadOnlyCollection<VisualHFT.Model.Order> ExecutedOrders { get; }
        ReadOnlyCollection<Position> Positions { get; }

        void SetSessionDate(DateTime? sessionDate);
        DateTime? GetSessionDate();

    }
}
