using Net.Leksi.ZkJson;
using org.apache.zookeeper;
using System.Reflection;
using System.Text.Json;

namespace Net.Leksi.MicroService.Common;

public class WorkerBuilder<TWorker, TConfig> 
where TWorker : TemplateWorker<TConfig> 
where TConfig : TemplateWorkerConfig, new()
{
    private readonly HostApplicationBuilder _builder;
    private WorkerBuilder() 
    {
        _builder = Host.CreateApplicationBuilder();
    }
    public static WorkerBuilder<TWorker, TConfig> Create(IConfiguration bootstrapConfig)
    {
        WorkerBuilder<TWorker, TConfig> result = new();
        if (bootstrapConfig[Constants.ConfigPropertyName] is null)
        {
            throw new Exception(string.Format(Constants.MissedMandatoryParam, Constants.ConfigPropertyName));
        }
        if (bootstrapConfig[Constants.NamePropertyName] is null)
        {
            throw new Exception(string.Format(Constants.MissedMandatoryParam, Constants.NamePropertyName));
        }

        ZkStart start = ZkStart.Create().WithConfiguration(bootstrapConfig);
        ZooKeeper? zooKeeper = start.Start() ?? throw new Exception(string.Format(Constants.ZookeeperConnectionFailed, start.ConnectionString));

        ZkJsonSerializer zkJson = new()
        {
            ZooKeeper = zooKeeper,
            Root = $"{bootstrapConfig[Constants.ConfigPropertyName]}/{bootstrapConfig[Constants.NamePropertyName]}",
        };

        JsonElement json = zkJson.IncrementalSerialize(Constants.ScriptPrefix);

        JsonSerializerOptions jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true, };
        jsonSerializerOptions.Converters.Add(new KafkaConfigJsonConverterFactory());

        result._builder.Configuration.AddConfiguration(bootstrapConfig);

        result._builder.Services.AddSingleton(zooKeeper);

        result._builder.Services.AddSingleton(services =>
        {
            TConfig conf = JsonSerializer.Deserialize<TConfig>(json, jsonSerializerOptions)!;
            services.GetRequiredService<LoggingManager>().Debug(
                string.Format("{0}: {1}", typeof(TConfig).FullName!, JsonSerializer.Serialize(conf))
            );
            return conf;
        });

        result.AddCloudClient(json, jsonSerializerOptions);

        result.AddKafkaProducer(json, jsonSerializerOptions);

        result.AddLogging(json, jsonSerializerOptions);

        result.AddWorkerId();

        result._builder.Services.AddHostedService<TWorker>();

        return result;
    }
    public IHost Build()
    {
        return _builder.Build();
    }
    private void AddCloudClient(JsonElement config, JsonSerializerOptions jsonSerializerOptions)
    {
        if (
            config.EnumerateObject().Where(
                e => e.Name.Equals(Constants.StorageParamName, StringComparison.OrdinalIgnoreCase)
            ).Select(e => e.Value).FirstOrDefault() is JsonElement json
            && json.ValueKind is not JsonValueKind.Undefined
        )
        {
            _builder.Services.AddSingleton<ICloudClient>(services =>
            {
                MinioConfig conf = JsonSerializer.Deserialize<MinioConfig>(json, jsonSerializerOptions)!;
                services.GetRequiredService<LoggingManager>().Debug(
                    string.Format("{0}: {1}", typeof(MinioConfig).FullName!, JsonSerializer.Serialize(conf))
                );
                return new MinioClientAdapter(conf);
            });
        }
    }
    private void AddKafkaProducer(JsonElement config, JsonSerializerOptions jsonSerializerOptions)
    {
        if (
            config.EnumerateObject().Where(
                e => e.Name.Equals(Constants.KafkaPropertyName, StringComparison.OrdinalIgnoreCase)
            ).Select(e => e.Value).FirstOrDefault() is JsonElement json
            && json.ValueKind is not JsonValueKind.Undefined
        )
        {
            _builder.Services.AddSingleton<IKafkaProducer>(services =>
            {
                KafkaConfig conf = JsonSerializer.Deserialize<KafkaConfig>(json, jsonSerializerOptions)!;
                services.GetRequiredService<LoggingManager>().Debug(
                    string.Format("{0}: {1}", typeof(KafkaConfig).FullName!, JsonSerializer.Serialize(conf))
                );
                return new KafkaProducerAdapter(services, conf);
            });
        }
    }
    private void AddLogging(JsonElement config, JsonSerializerOptions jsonSerializerOptions)
    {
        if (
            config.EnumerateObject().Where(
                e => e.Name.Equals(Constants.LoggingPropertyName, StringComparison.OrdinalIgnoreCase)
            ).Select(e => e.Value).FirstOrDefault() is JsonElement json
            && json.ValueKind is not JsonValueKind.Undefined
        )
        {
            LoggingManager manager = new();
            manager.Configure(_builder.Logging, json, jsonSerializerOptions);
            _builder.Services.AddSingleton(services =>
            {
                manager.SetServices(services);
                return manager;
            });
        }
    }
    private void AddWorkerId()
    {
        _builder.Services.AddSingleton(services =>
        {
            string? id = null;
            string? name = null;

            if (services.GetService<IConfiguration>() is IConfiguration conf)
            {
                if (bool.TryParse(conf["RUNNING_IN_CONTAINER"], out bool ric) && ric && conf["HOSTNAME"] is string workerId)
                {
                    id = workerId;
                }
                if (conf[Constants.NamePropertyName] is string name1)
                {
                    name = name1;
                }
            }
            id ??= Guid.NewGuid().ToString();
            name ??= Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? Assembly.GetExecutingAssembly().GetName().Name ?? "Undefined";
            WorkerId result = new() { Id = id, Name = name };

            return result;
        });
    }
}
