

using Net.Leksi.MicroService.Common;

namespace Net.Leksi.MicroService.LogSaver;

public class Worker : TemplateWorker<Config>
{
    private readonly KafkaConsumer _kafkaConsumer;
    protected override bool IsOperative => true;
    public Worker(IServiceProvider services) : base(services)
    {
        IsSingleton = true;
        _kafkaConsumer = _services.GetRequiredKeyedService<KafkaConsumer>("kafka");
        _kafkaConsumer.Consume += _kafkaConsumer_Consume;
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
        _kafkaConsumer.Listen((int)Config.PollPeriod!, stoppingToken);
        UpdateState();
        await Task.CompletedTask;
    }
    protected override async Task ProcessInoperativeError(TimeSpan inoperativeTime, Exception? lastInoperativeException, CancellationToken stoppingToken)
    {
        await Task.CompletedTask;
    }
    protected override async Task ProcessInoperativeWarning(TimeSpan inoperativeTime, Exception? lastInoperativeException, CancellationToken stoppingToken)
    {
        await Task.CompletedTask;
    }
    private void _kafkaConsumer_Consume(KafkaConsumeEventArgs args)
    {
        Console.WriteLine($"{args.ConsumeResult.Key}: {args.ConsumeResult.Message}");
    }

}
