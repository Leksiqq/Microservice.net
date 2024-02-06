using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using static Confluent.Kafka.ConfigPropertyNames;

namespace Net.Leksi.MicroService.Common;

public class KafkaLoggerProvider : ILoggerProvider
{
    private readonly KafkaLoggerConfig _config;
    public KafkaLoggerProvider(KafkaLoggerConfig config)
    {
        _config = config;
    }
    public ILogger CreateLogger(string categoryName)
    {
        return new KafkaLogger(_config);
    }
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
