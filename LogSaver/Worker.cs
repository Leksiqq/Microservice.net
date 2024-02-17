

using Confluent.Kafka;
using MongoDB.Bson;
using MongoDB.Driver;
using Net.Leksi.MicroService.Common;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Net.Leksi.MicroService.LogSaver;

public class Worker : TemplateWorker<Config>
{
    private readonly KafkaConsumer _kafkaConsumer;
    private readonly IMongoCollection<BsonDocument> _mongoCollection;
    private readonly JsonSerializerOptions _messageJsonOptions = new();
    protected override bool IsOperative => true;
    public Worker(IServiceProvider services) : base(services)
    {
        IsSingleton = true;
        _kafkaConsumer = _services.GetRequiredKeyedService<KafkaConsumer>("kafka");
        _mongoCollection = _services.GetRequiredKeyedService<IMongoCollection<BsonDocument>>("mongodb");
        _kafkaConsumer.Consume += _kafkaConsumer_Consume;
        _messageJsonOptions.Converters.Add(new KafkaMessageConverterFactory());
    }
    protected override async Task Exiting(CancellationToken stoppingToken)
    {
        _kafkaConsumer.Dispose();
        await Task.CompletedTask;
    }
    protected override async Task Initialize(CancellationToken stoppingToken)
    {
        await Task.CompletedTask;
    }
    protected override async Task MakeOperative(CancellationToken stoppingToken)
    {
        await Task.CompletedTask;
    }
    protected override async Task Operate(CancellationToken stoppingToken)
    {
        try
        {
            _kafkaConsumer.Listen((int)Config.PollPeriod!, stoppingToken);
            UpdateState();
        }
        catch (ConsumeException ex) 
        {
            Console.WriteLine(ex);
        }
        await Task.CompletedTask;
    }
    private void _kafkaConsumer_Consume(KafkaConsumeEventArgs args)
    {
        Console.WriteLine(args.ConsumeResult.Message.Value);
        KafkaLogMessage message = JsonSerializer.Deserialize<KafkaLogMessage>(args.ConsumeResult.Message.Value, _messageJsonOptions)!;
        //_mongoCollection.InsertOne(message.ToBsonDocument());
    }

}
