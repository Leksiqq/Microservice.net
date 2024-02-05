using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common;

public class KafkaLoggerProvider : ILoggerProvider
{
    private readonly KafkaLoggerConfig _config;
    private readonly IProducer<string, string> _producer = null!;

    public KafkaLoggerProvider(KafkaLoggerConfig config)
    {
        _config = config;
    }

    public ILogger CreateLogger(string categoryName)
    {
        Console.WriteLine(categoryName);
        return new KafkaLogger(this);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
