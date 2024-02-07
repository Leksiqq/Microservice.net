using System.Text.Json;

namespace Net.Leksi.MicroService.Common;

public static class Extensions
{
    public static HostApplicationBuilder AddCloudClient(this HostApplicationBuilder builder, JsonElement config, JsonSerializerOptions jsonSerializerOptions)
    {
        if(
            config.EnumerateObject().Where(
                e => e.Name.Equals(Constants.StorageParamName, StringComparison.OrdinalIgnoreCase)
            ).Select(e => e.Value).FirstOrDefault() is JsonElement json
            && json.ValueKind is not JsonValueKind.Undefined
        )
        {
            builder.Services.AddSingleton<ICloudClient>(services =>
            {
                MinioConfig conf = JsonSerializer.Deserialize<MinioConfig>(json, jsonSerializerOptions)!;
                services.GetRequiredService<LoggingManager>().Debug(typeof(MinioConfig).FullName!, JsonSerializer.Serialize(conf));
                return new MinioClientAdapter(conf);
            });
        }
        return builder;
    }
    public static HostApplicationBuilder AddKafkaProducer<TMessage>(this HostApplicationBuilder builder, JsonElement config, JsonSerializerOptions jsonSerializerOptions) 
    where TMessage : class
    {
        if (
            config.EnumerateObject().Where(
                e => e.Name.Equals(Constants.KafkaPropertyName, StringComparison.OrdinalIgnoreCase)
            ).Select(e => e.Value).FirstOrDefault() is JsonElement json
            && json.ValueKind is not JsonValueKind.Undefined
        )
        {
            builder.Services.AddSingleton<IKafkaProducer<TMessage>>(services =>
            {
                KafkaConfig conf = JsonSerializer.Deserialize<KafkaConfig>(json, jsonSerializerOptions)!;
                services.GetRequiredService<LoggingManager>().Debug(typeof(KafkaConfig).FullName!, JsonSerializer.Serialize(conf));
                return new KafkaProducerAdapter<TMessage>(conf);
            });
        }
        return builder;
    }
    public static HostApplicationBuilder AddLogging(this HostApplicationBuilder builder, JsonElement config, JsonSerializerOptions jsonSerializerOptions)
    {
        if(
            config.EnumerateObject().Where(
                e => e.Name.Equals(Constants.LoggingPropertyName, StringComparison.OrdinalIgnoreCase)
            ).Select(e => e.Value).FirstOrDefault() is JsonElement json
            && json.ValueKind is not JsonValueKind.Undefined
        )
        {
            LoggingManager manager = new();
            manager.Configure(builder.Logging, json, jsonSerializerOptions);
            builder.Services.AddSingleton(services =>
            {
                manager.SetLoggerFactory(services.GetRequiredService<ILoggerFactory>());
                return manager;
            });
        }
        return builder;
    }
}
