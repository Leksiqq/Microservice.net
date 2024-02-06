using Confluent.Kafka;

namespace Net.Leksi.MicroService.Common;

public interface IKafkaProducer<TMessage>: IDisposable where TMessage : class
{
    Task<List<DeliveryResult<string, string>>> ProduceAsync(string key, TMessage message, CancellationToken stoppingToken);
}
