using Net.Leksi.MicroService.Common;
using Net.Leksi.MicroService.ImapReader;


IConfiguration bootstrapConfig = new ConfigurationBuilder()
    .AddCommandLine(args)
    .AddEnvironmentVariables()
    .Build();

WorkerBuilder<Worker, Config>.Create(bootstrapConfig).Build().Run();

