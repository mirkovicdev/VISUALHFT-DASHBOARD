using log4net;
using log4net.Config;
using System.IO;
using System.Reflection;

public class Log4NetFixture
{
    public Log4NetFixture()
    {
        var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
        XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
    }
}
