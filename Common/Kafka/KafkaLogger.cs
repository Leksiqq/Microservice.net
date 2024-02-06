using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common;

public class KafkaLogger : ILogger, IDisposable
{
    private readonly KafkaLoggerConfig _config;
    private readonly IProducer<string, string> _producer = null!;
    public KafkaLogger(KafkaLoggerConfig config)
    {
        _config = config;
        _producer = new ProducerBuilder<string, string>(_config.Properties).Build();
    }
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return this;
    }
    public bool IsEnabled(LogLevel logLevel)
    {
        return GetTopics(logLevel) is List<string> list && list.Count > 0;
    }
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (GetTopics(logLevel) is List<string> list && list.Count > 0)
        {
            Message<string, string> message = new() { };
        }
        Console.WriteLine($"{logLevel}, {eventId}, {typeof(TState)}, {state}, {exception}, {formatter(state, exception)}");
    }
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
    private List<string>? GetTopics(LogLevel logLevel)
    {
        switch(logLevel)
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
