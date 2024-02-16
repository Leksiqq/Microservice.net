using Net.Leksi.MicroService.Common;
using Net.Leksi.MicroService.FtpReader;

IConfiguration bootstrapConfig = new ConfigurationBuilder()
    .AddCommandLine(args)
    .AddEnvironmentVariables()
    .Build();

WorkerBuilder<Worker, Config>.Create(bootstrapConfig)
    .AddKafkaProducer("kafka")
    .AddCloudClient("storage")
    .Build()
    .Run();

