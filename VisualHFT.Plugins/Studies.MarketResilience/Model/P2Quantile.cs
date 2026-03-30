using System;

namespace Studies.MarketResilience.Model
{
    // Minimal P² quantile estimator (Jain & Chlamtac, 1985)
    // Suitable for single quantiles like 0.5 (median) and 0.9 (p90)
    // Minimal, safe P² quantile estimator (Jain & Chlamtac, 1985)
    internal sealed class P2Quantile
    {
        private readonly double _p;
        private int _count;
        private readonly double[] q = new double[5];
        private readonly double[] n = new double[5];
        private readonly double[] np = new double[5];
        private readonly double[] dn = new double[5];

        public P2Quantile(double p)
        {
            if (p <= 0 || p >= 1) throw new ArgumentOutOfRangeException(nameof(p));
            _p = p;
        }

        public int Count => _count;
        public double Estimate => _count < 5 ? (_count == 0 ? 0.0 : q[Math.Min(_count - 1, 4)]) : q[2];

        public void Observe(double x)
        {
            // ✅ NEW: Guard against invalid inputs
            if (double.IsNaN(x) || double.IsInfinity(x))
            {
                // Silently ignore invalid values to maintain robustness
                // Alternative: throw new ArgumentException($"Invalid value: {x}", nameof(x));
                return;
            }
            if (_count < 5)
            {
                q[_count++] = x;
                if (_count == 5)
                {
                    Array.Sort(q);               // initialize markers
                    for (int i = 0; i < 5; i++) n[i] = i + 1;
                    np[0] = 1;
                    np[1] = 1 + 2 * _p;
                    np[2] = 1 + 4 * _p;
                    np[3] = 3 + 2 * _p;
                    np[4] = 5;
                    dn[0] = 0;
                    dn[1] = _p / 2;
                    dn[2] = _p;
                    dn[3] = (1 + _p) / 2;
                    dn[4] = 1;
                }
                return;
            }

            // Find cell k and update extreme markers
            int k;
            if (x < q[0]) { q[0] = x; k = 0; }
            else if (x < q[1]) k = 0;
            else if (x < q[2]) k = 1;
            else if (x < q[3]) k = 2;
            else if (x < q[4]) k = 3;
            else { q[4] = x; k = 3; }

            // Update positions of markers above k
            for (int i = k + 1; i < 5; i++) n[i] += 1;

            // Desired marker positions
            for (int i = 0; i < 5; i++) np[i] += dn[i];

            // ✅ FIX: Adjust heights of interior markers using CORRECT parabolic formula
            for (int i = 1; i <= 3; i++)
            {
                double d = np[i] - n[i];

                if ((d >= 1 && n[i + 1] - n[i] > 1) || (d <= -1 && n[i - 1] - n[i] < -1))
                {
                    int sign = Math.Sign(d);

                    // ✅ CORRECT P² parabolic formula (Jain & Chlamtac, 1985)
                    double qPar = q[i] + (sign / (n[i + 1] - n[i - 1])) * (
                        (n[i] - n[i - 1] + sign) * (q[i + 1] - q[i]) / (n[i + 1] - n[i]) +
                        (n[i + 1] - n[i] - sign) * (q[i] - q[i - 1]) / (n[i] - n[i - 1])
                    );

                    // If parabolic prediction is between neighbors, use it; else use linear
                    if (q[i - 1] < qPar && qPar < q[i + 1])
                    {
                        q[i] = qPar;
                    }
                    else
                    {
                        // ✅ CORRECT linear formula
                        q[i] += sign * (q[i + sign] - q[i]) / (n[i + sign] - n[i]);
                    }

                    n[i] += sign;
                }
            }

            _count++;
        }
    }

}
