using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

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
        configuration["auto.offset.reset"] = "earliest";
        configuration["enable.auto.commit"] = "false";
        _consumer = new ConsumerBuilder<string, string>(configuration.AsEnumerable()).Build();
    }
    public void Listen(int timeout, CancellationToken stoppingToken)
    {
        using CancellationTokenSource timeoutCts = new(timeout == Timeout.Infinite ? _config.Timeout : timeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken, 
            timeoutCts?.Token ?? CancellationToken.None
        );
        DateTime started = DateTime.Now;
        while (!linkedCts.IsCancellationRequested)
        {
            try
            {
                _consumer.Subscribe(_config.Topic ?? throw new InvalidOperationException($"Topic is missed!"));
                ConsumeResult<string, string> cr = (_consumer.Consume(linkedCts.Token));
                if (!linkedCts.IsCancellationRequested && cr is { })
                {
                    Consume?.Invoke(new KafkaConsumeEventArgs { ConsumeResult = cr });
                }
                _consumer.Commit(cr);
                if (timeout != Timeout.Infinite && (DateTime.Now - started).TotalMilliseconds > timeout)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                if (!linkedCts.Token.IsCancellationRequested)
                {
                    throw;
                }
            }
            finally
            {
                _consumer.Unsubscribe();
            }
        }
    }
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _consumer?.Dispose();
    }
}
