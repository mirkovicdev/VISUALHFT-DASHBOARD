using VisualHFT.Commons.Model;
using VisualHFT.Enums;

namespace VisualHFT.Model
{
    public partial class Execution : IResettable
    {

        public long OrderID { get; set; }
        public int ExecutionID { get; set; }
        public string ClOrdId { get; set; }
        public string ExecID { get; set; }
        public System.DateTime LocalTimeStamp { get; set; }
        public System.DateTime ServerTimeStamp { get; set; }
        public Nullable<decimal> Price { get; set; }
        public int ProviderID { get; set; }
        public Nullable<decimal> QtyFilled { get; set; }
        public eORDERSIDE Side { get; set; }
        public eORDERSTATUS Status { get; set; }
        public bool IsOpen { get; set; }


        public string ProviderName { get; set; }
        public string Symbol { get; set; }
        public double LatencyInMiliseconds
        {
            get { return this.LocalTimeStamp.Subtract(this.ServerTimeStamp).TotalMilliseconds; }
        }
        public string OrigClOrdID { get; set; }

        public void Reset()
        {
            this.OrderID = 0;
            this.ClOrdId = "";
            this.ExecID = "";
            this.ExecutionID = 0;
            this.IsOpen = true;
            this.LocalTimeStamp = DateTime.MinValue;
            this.Price = 0;
            this.ProviderID = 0;
            this.QtyFilled = 0;
            this.ServerTimeStamp = DateTime.MinValue;
            this.Side = eORDERSIDE.None;
            this.Status = eORDERSTATUS.NONE;
            this.Symbol = "";
        }
    }
}
