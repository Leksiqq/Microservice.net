using Net.Leksi.MicroService.Common;
using Net.Leksi.MicroService.LogSaver;


IConfiguration bootstrapConfig = new ConfigurationBuilder()
    .AddCommandLine(args)
    .AddEnvironmentVariables()
    .Build();

WorkerBuilder<Worker, Config>.Create(bootstrapConfig)
    .AddKafkaConsumer("kafka")
    .AddCloudClient("storage")
    .Build()
    .Run();

