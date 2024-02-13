using Confluent.Kafka;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;

namespace Net.Leksi.MicroService.Common;

public class KafkaProducerBase: IDisposable
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new() 
    { 
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };
    private readonly IProducer<string, string> _producer = null!;
    private readonly IServiceProvider? _services;
    protected readonly KafkaConfigBase _config;
    protected readonly WorkerId? _workerId;
    private bool _convertersAdded = false;
    public KafkaProducerBase(IServiceProvider? services, KafkaConfigBase config)
    {
        _services = services;
        _workerId = _services?.GetRequiredService<WorkerId>();
        _config = config;
        _producer = new ProducerBuilder<string, string>(_config.Properties).Build();

    }
    public async Task<List<DeliveryResult<string, string>>> ProduceAsync<TMessage>(TMessage message, List<string> topics, CancellationToken stoppingToken)
        where TMessage : KafkaMessageBase
    {
        if(_config.Sender is { })
        {
            message.Sender = _config.Sender;
        }
        if(_workerId is { })
        {
            message.WorkerId = _workerId;
        }
        if (!_convertersAdded)
        {
            message.AddConverters(_jsonSerializerOptions.Converters);
            _convertersAdded = true;
        }
        List<DeliveryResult<string, string>> result = [];
        MemoryStream ms = new();
        JsonSerializer.Serialize(ms, message, _jsonSerializerOptions);
        ms.Position = 0;
        Message<string, string> kafkaMessage = new()
        { 
            Value = new StreamReader(ms).ReadToEnd(), 
            Headers = [] 
        };
        if((message.Key ?? _config.Sender ?? _workerId?.Name) is string keyStr)
        {
            kafkaMessage.Key = keyStr;
        }

        kafkaMessage.Headers.Add(Constants.KafkaMessageTypeName, Encoding.UTF8.GetBytes(typeof(TMessage).FullName!));

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
