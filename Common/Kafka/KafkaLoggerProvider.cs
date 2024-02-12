using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common;

public class KafkaLoggerProvider(KafkaLoggerConfig config, Func<string?, string?, LogLevel, bool> filter, Func<IServiceProvider?> getServices) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new KafkaLogger(config, categoryName, filter, getServices);
    }
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
