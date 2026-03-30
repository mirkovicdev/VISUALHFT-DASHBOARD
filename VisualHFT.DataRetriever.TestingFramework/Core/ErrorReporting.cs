using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualHFT.DataRetriever.TestingFramework.Core
{
    public class ErrorReporting
    {
        public string PluginName { get; internal set; }
        public string Message { get; internal set; }
        public ErrorMessageTypes MessageType { get; internal set; }
    }
}
