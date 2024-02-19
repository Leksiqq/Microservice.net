using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
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
    protected readonly KafkaProducerConfig _config;
    protected readonly WorkerId? _workerId;
    public KafkaProducerBase(IServiceProvider? services, KafkaProducerConfig config)
    {
        _services = services;
        _workerId = _services?.GetRequiredService<WorkerId>();
        _config = config;
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(_config.Properties!)
            .Build();
        _producer = new ProducerBuilder<string, string>(configuration.AsEnumerable()).Build();
        _jsonSerializerOptions.Converters.Add(new KafkaMessageConverterFactory());
    }
    public void AddConverter(JsonConverter converter)
    {
        _jsonSerializerOptions.Converters.Add(converter);
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
        message.Timestamp = DateTime.UtcNow;
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

        using CancellationTokenSource timeoutCts = new(_config.Timeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeoutCts.Token);

        foreach (string topic in topics)
        {
            result.Add(await _producer.ProduceAsync(topic, kafkaMessage, linkedCts.Token));
        }

        return result;
    }
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _producer?.Dispose();
    }
}
