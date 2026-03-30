using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualHFT.Commons.Exceptions
{
    public class ExceptionSequenceNotSupportedByExchange: Exception
    {
        public ExceptionSequenceNotSupportedByExchange() : base("This exchange does not support sequence numbers.")
        {
        }

        public ExceptionSequenceNotSupportedByExchange(string message) : base(message)
        {
        }

        public ExceptionSequenceNotSupportedByExchange(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
