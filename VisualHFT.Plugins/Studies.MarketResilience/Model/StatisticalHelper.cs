using System;
using System.Linq;
using VisualHFT.Commons.Pools;

namespace Studies.MarketResilience.Model
{
    public static class StatisticalHelper
    {
        public static decimal Average(this RollingWindow<decimal> window)
            => window.Count == 0 ? 0 : window.Items.Average();

        public static decimal StandardDeviation(this RollingWindow<decimal> window)
        {
            if (window.Count == 0) return 0;
            var avg = window.Average();
            var variance = window.Items.Average(x => (x - avg) * (x - avg));
            return (decimal)Math.Sqrt((double)variance);
        }

        public static double Average(this RollingWindow<double> window)
            => window.Count == 0 ? 0 : window.Items.Average();

        public static double StandardDeviation(this RollingWindow<double> window)
        {
            if (window.Count == 0) return 0;
            var avg = window.Average();
            var variance = window.Items.Average(x => (x - avg) * (x - avg));
            return Math.Sqrt(variance);
        }
    }

}
