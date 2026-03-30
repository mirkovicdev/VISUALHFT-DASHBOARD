using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualHFT.Commons.Exceptions
{
    public class ExceptionDeltasNotSupportedByExchange : Exception
    {
        public ExceptionDeltasNotSupportedByExchange() : base("Deltas are not supported for this exchange.")
        {
        }

        public ExceptionDeltasNotSupportedByExchange(string message) : base(message)
        {
        }

        public ExceptionDeltasNotSupportedByExchange(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
