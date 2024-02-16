using Confluent.Kafka;
using Microsoft.Extensions.Configuration;

namespace Net.Leksi.MicroService.Common;

public class KafkaConsumer : IDisposable
{
    public event KafkaConsumeEventHandler? Consume;

    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceProvider? _services;
    protected readonly KafkaConsumerConfig _config;
    public KafkaConsumer(IServiceProvider services, KafkaConsumerConfig config)
    {
        _services = services;
        _config = config;
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(_config.Properties.AsEnumerable()!)
            .Build();

        configuration["group.id"] = _config.Group ?? throw new InvalidOperationException($"Group is missed!");
        _consumer = new ConsumerBuilder<string, string>(configuration.AsEnumerable()).Build();
        _consumer.Subscribe(_config.Topic ?? throw new InvalidOperationException($"Topic is missed!"));
    }
    public void Listen(int timeout, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string> cr = _consumer.Consume(timeout);
            if (!stoppingToken.IsCancellationRequested && cr is { })
            {
                Consume?.Invoke(new KafkaConsumeEventArgs { ConsumeResult = cr });
            }
        }
    }
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _consumer?.Dispose();
    }
}
