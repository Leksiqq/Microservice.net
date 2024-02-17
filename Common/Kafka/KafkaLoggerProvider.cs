using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common;

public class KafkaLoggerProvider(KafkaLoggerConfig config, Func<string?, string?, LogLevel, bool> filter, Func<IServiceProvider?> getServices) : ILoggerProvider
{
    private bool _isOperative = true;
    public ILogger CreateLogger(string categoryName)
    {
        return new KafkaLogger(config, categoryName, filter, getServices, Operative);
    }
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
    private bool Operative(bool? newValue)
    {
        if(newValue is bool b)
        {
            _isOperative = b;
        }
        return _isOperative;
    }
}
