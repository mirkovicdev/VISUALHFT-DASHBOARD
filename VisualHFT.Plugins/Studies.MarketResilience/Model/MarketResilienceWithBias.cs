using System;
using VisualHFT.Enums;
using VisualHFT.Studies.MarketResilience.Model;

namespace Studies.MarketResilience.Model
{

    public class MarketResilienceWithBias : MarketResilienceCalculator
    {
        private const double MRB_ON = 0.30;  // only speak when resilience clearly poor
        private const double MRB_OFF = 0.50;  // stop speaking once resilience improves
        private bool _mrbArmed = false;       // hysteresis latch

        public MarketResilienceWithBias(PlugInSettings settings): base(settings)
        {

        }
        protected override eMarketBias? CalculateMRBias()
        {
            // Need depth stamps
            if (ShockDepth == null || RecoveredDepth == null) return null;
            if (ShockDepth.Value == eLOBSIDE.NONE || RecoveredDepth.Value == eLOBSIDE.NONE) return null;

            // Let MR drive MRB. If MR is unknown (timeout), do not emit bias.
            double mr = (double)CurrentMRScore;

            // Hysteresis: arm when MR ≤ MRB_ON; disarm when MR ≥ MRB_OFF
            if (!_mrbArmed && mr <= MRB_ON) _mrbArmed = true;
            if (_mrbArmed && mr >= MRB_OFF) { _mrbArmed = false; return eMarketBias.Neutral; }

            if (!_mrbArmed) return null; // don’t speak when resilience is middling/good

            // Directional mapping: only control transfer yields bias
            var shock = ShockDepth.Value;
            var rec = RecoveredDepth.Value;

            if (shock == eLOBSIDE.BOTH && rec == eLOBSIDE.BID) return eMarketBias.Bullish;
            if (shock == eLOBSIDE.BOTH && rec == eLOBSIDE.ASK) return eMarketBias.Bearish;
            if ((shock & eLOBSIDE.BID) != 0 && (rec & eLOBSIDE.ASK) != 0) return eMarketBias.Bearish; // BID→ASK
            if ((shock & eLOBSIDE.ASK) != 0 && (rec & eLOBSIDE.BID) != 0) return eMarketBias.Bullish; // ASK→BID

            // Same-side recovery or BOTH→BOTH ⇒ resilience, thus neutral
            return eMarketBias.Neutral;
        }
    }
}
