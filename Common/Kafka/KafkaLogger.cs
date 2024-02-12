using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common;

public class KafkaLogger(
    KafkaLoggerConfig config, 
    string category, 
    Func<string?, string?, LogLevel, bool> filter, 
    Func<IServiceProvider?> getServices
) : ILogger, IDisposable
{
    private KafkaProducerBase? _producer;
    private IServiceProvider? _services;
    private LogLevel _minLogLevel;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return this;
    }
    public bool IsEnabled(LogLevel logLevel)
    {
        if(logLevel < _minLogLevel)
        {
            if(filter(nameof(KafkaLoggerProvider), category, logLevel))
            {
                _minLogLevel = logLevel;
            }
        }
        return logLevel >= _minLogLevel && GetTopics(logLevel) is List<string> list && list.Count > 0; 
    }
    public void Log<TState>(
        LogLevel logLevel, 
        EventId eventId, 
        TState state, 
        Exception? exception, 
        Func<TState, Exception?, string> formatter
    )
    {
        if (IsEnabled(logLevel))
        {
            if(_services is null && getServices() is IServiceProvider services)
            {
                _services = services;
                _producer?.Dispose();
                _producer = null;
            }
            _producer ??= new KafkaProducerBase(_services, config);
            _ = _producer.ProduceAsync(new KafkaLogMessage {
                EventId = eventId,
                Exception = exception,
                LogLevel = logLevel,
                Message = formatter(state, exception),
                State = state,
            }, GetTopics(logLevel)!, CancellationToken.None).Result;
        }
    }
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _producer?.Dispose();
    }
    private List<string>? GetTopics(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Debug       => config.DebugTopics,
            LogLevel.Information => config.InformationTopics,
            LogLevel.Warning     => config.WarningTopics,
            LogLevel.Error       => config.ErrorTopics,
            LogLevel.Critical    => config.CriticalTopics,
            LogLevel.Trace       => config.TraceTopics,
            _ => null,
        };
    }
}
