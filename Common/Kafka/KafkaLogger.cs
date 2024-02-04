using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common;

public class KafkaLogger : ILogger, IDisposable
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return this;
    }
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Console.WriteLine($"{logLevel}, {eventId}, {state}, {exception}, {formatter(state, exception)}");
    }
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
