using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualHFT.Commons.Exceptions
{
    public class ExceptionScenarioNotSupportedByExchange : Exception
    {
        public ExceptionScenarioNotSupportedByExchange() : base("This scenario is not valid for this exchange.")
        {
        }

        public ExceptionScenarioNotSupportedByExchange(string message) : base(message)
        {
        }

        public ExceptionScenarioNotSupportedByExchange(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
