using Confluent.Kafka;
using System.Text.Json;
using System.Text;

namespace Net.Leksi.MicroService.Common;

public class KafkaProducerBase<TMessage>: IDisposable where TMessage : class
{
    private readonly IProducer<string, string> _producer = null!;
    private readonly string _messageTypeName = typeof(TMessage).FullName!;
    protected readonly KafkaConfig _config;
    public KafkaProducerBase(KafkaConfig config)
    {
        _config = config;
        _producer = new ProducerBuilder<string, string>(_config.Properties).Build();
    }
    protected async Task<List<DeliveryResult<string, string>>> ProduceAsync(string key, TMessage message, List<string> topics, CancellationToken stoppingToken)
    {
        List<DeliveryResult<string, string>> result = [];
        MemoryStream ms = new();
        JsonSerializer.Serialize(ms, message);
        ms.Position = 0;
        Message<string, string> kafkaMessage = new() { Key = key, Value = new StreamReader(ms).ReadToEnd() };

        kafkaMessage.Headers = [];
        kafkaMessage.Headers.Add(nameof(_config.Sender), Encoding.UTF8.GetBytes(_config.Sender));
        kafkaMessage.Headers.Add(Constants.KafkaMessageTypeName, Encoding.UTF8.GetBytes(_messageTypeName));


        foreach (string topic in topics)
        {
            result.Add(await _producer.ProduceAsync(topic, kafkaMessage, stoppingToken));
        }
        return result;
    }
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _producer?.Dispose();
    }
}
