using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketConnectors.Gemini.Model
{
    public class ChangeEntry
    {
        public string Side { get; set; } // Or OrderSide enum
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
    }
}
