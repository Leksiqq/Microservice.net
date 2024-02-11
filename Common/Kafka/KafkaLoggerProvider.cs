using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common;

public class KafkaLoggerProvider : ILoggerProvider
{
    private readonly KafkaLoggerConfig _config;
    private readonly Func<string?, string?, LogLevel, bool> _filter;
    public KafkaLoggerProvider(KafkaLoggerConfig config, Func<string?,string?, LogLevel,bool> filter)
    {
        _config = config;
        _filter = filter;
    }
    public ILogger CreateLogger(string categoryName)
    {
        return new KafkaLogger(_config, categoryName, _filter);
    }
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
