using Net.Leksi.MicroService.Common;
using Net.Leksi.MicroService.FtpReader;

IConfiguration bootstrapConfig = new ConfigurationBuilder()
    .AddCommandLine(args)
    .AddEnvironmentVariables()
    .Build();

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

WorkerBuilder<Worker, Config>.Create(bootstrapConfig).Build().Run();

