using VisualHFT.Commons.Model;
using VisualHFT.Enums;

namespace VisualHFT.Commons.Interfaces
{
    public interface IDataRetrieverTestable
    {
        void InjectSnapshot(VisualHFT.Model.OrderBook snapshotModel, long sequence);
        void InjectDeltaModel(List<DeltaBookItem> bidDeltaModel, List<DeltaBookItem> askDeltaModel);
        List<VisualHFT.Model.Order> ExecutePrivateMessageScenario(eTestingPrivateMessageScenario scenario);
    }
}
