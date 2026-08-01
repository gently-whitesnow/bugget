using Serilog;

namespace Bugget.Api.Logging;

public static class BootstrapLogger
{
    public static Serilog.ILogger Create()
    {
        return new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
    }
}
