using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common;

public class KafkaLogger : KafkaProducerBase<KafkaLogMessage>, ILogger, IDisposable
{
    private readonly new KafkaLoggerConfig _config;
    private readonly string _category;
    private readonly Func<string?, string?, LogLevel, bool> _filter;
    private LogLevel _minLogLevel;

    public KafkaLogger(KafkaLoggerConfig config, string category, Func<string?, string?, LogLevel, bool> filter): base((KafkaConfigBase)config)
    {
        _config = config;
        _category = category;
        _filter = filter;
    }
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return this;
    }
    public bool IsEnabled(LogLevel logLevel)
    {
        if(logLevel < _minLogLevel)
        {
            if(_filter(nameof(KafkaLoggerProvider), _category, logLevel))
            {
                _minLogLevel = logLevel;
            }
        }
        return logLevel >= _minLogLevel && GetTopics(logLevel) is List<string> list && list.Count > 0; 
    }
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            _ = ProduceAsync(_config.Sender, new KafkaLogMessage {
                Sender = _config.Sender,
                EventId = eventId,
                Exception = exception,
                LogLevel = logLevel,
                Message = formatter(state, exception),
                State = state,
            }, GetTopics(logLevel)!, CancellationToken.None).Result;
            Console.WriteLine($"{logLevel}, {eventId}, {typeof(TState)}, {state}, {exception}, {formatter(state, exception)}");
        }
    }
    private List<string>? GetTopics(LogLevel logLevel)
    {
        switch (logLevel)
        {
            case LogLevel.Debug:
                return _config.DebugTopics;
            case LogLevel.Information:
                return _config.InformationTopics;
            case LogLevel.Warning:
                return _config.WarningTopics;
            case LogLevel.Error:
                return _config.ErrorTopics;
            case LogLevel.Critical:
                return _config.CriticalTopics;
            case LogLevel.Trace:
                return _config.TraceTopics;
        }
        return null;
    }
}
