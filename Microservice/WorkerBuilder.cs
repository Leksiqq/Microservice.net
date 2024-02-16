using Net.Leksi.ZkJson;
using org.apache.zookeeper;
using System.Reflection;
using System.Text.Json;

namespace Net.Leksi.MicroService.Common;

public class WorkerBuilder<TWorker, TConfig> 
where TWorker : TemplateWorker<TConfig> 
where TConfig : TemplateWorkerConfig, new()
{
    private const string s_debugFormat = "{0}: {1}";
    private readonly HostApplicationBuilder _builder;
    private JsonElement _json;
    private JsonSerializerOptions _jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true, };
    private WorkerBuilder() 
    {
        _builder = Host.CreateApplicationBuilder();
        _jsonSerializerOptions.Converters.Add(new KafkaConfigJsonConverterFactory());
        _builder.Services.AddTransient<StoredFolder>();
        _builder.Services.AddHostedService<TWorker>();
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

        result._json = zkJson.IncrementalSerialize(Constants.ScriptPrefix);


        result._builder.Configuration.AddConfiguration(bootstrapConfig);

        result._builder.Services.AddSingleton(zooKeeper);

        result.AddConfig();

        result.AddLogging();

        result.AddWorkerId();

        return result;
    }
    public WorkerBuilder<TWorker, TConfig> AddKafkaConsumer(string serviceKey)
    {
        if (
            _json.EnumerateObject().Where(
                e => e.Name.Equals(serviceKey, StringComparison.OrdinalIgnoreCase)
            ).Select(e => e.Value).FirstOrDefault() is JsonElement json
            && json.ValueKind is not JsonValueKind.Undefined
        )
        {
            _builder.Services.AddKeyedSingleton(serviceKey, (services, _) =>
            {
                KafkaConsumerConfig conf = JsonSerializer.Deserialize<KafkaConsumerConfig>(json, _jsonSerializerOptions)!;
                services.GetRequiredService<LoggingManager>().Debug(
                    string.Format(s_debugFormat, typeof(KafkaProducerConfig).FullName!, JsonSerializer.Serialize(conf))
                );
                return new KafkaConsumer(services, conf);
            });
        }
        return this;
    }
    public WorkerBuilder<TWorker, TConfig> AddCloudClient(string serviceKey)
    {
        if (
            _json.EnumerateObject().Where(
                e => e.Name.Equals(serviceKey, StringComparison.OrdinalIgnoreCase)
            ).Select(e => e.Value).FirstOrDefault() is JsonElement json
            && json.ValueKind is not JsonValueKind.Undefined
        )
        {
            _builder.Services.AddKeyedSingleton<ICloudClient>(string.Intern(serviceKey), (services, sk) =>
            {
                MinioConfig conf = JsonSerializer.Deserialize<MinioConfig>(json, _jsonSerializerOptions)!;
                services.GetRequiredService<LoggingManager>().Debug(
                    string.Format(s_debugFormat, typeof(MinioConfig).FullName!, JsonSerializer.Serialize(conf))
                );
                return new MinioClientAdapter(conf);
            });
        }
        return this;
    }
    public WorkerBuilder<TWorker, TConfig> AddKafkaProducer(string serviceKey)
    {
        if (
            _json.EnumerateObject().Where(
                e => e.Name.Equals(serviceKey, StringComparison.OrdinalIgnoreCase)
            ).Select(e => e.Value).FirstOrDefault() is JsonElement json
            && json.ValueKind is not JsonValueKind.Undefined
        )
        {
            _builder.Services.AddKeyedSingleton(string.Intern(serviceKey), (services, sk) =>
            {
                KafkaProducerConfig conf = JsonSerializer.Deserialize<KafkaProducerConfig>(json, _jsonSerializerOptions)!;
                services.GetRequiredService<LoggingManager>().Debug(
                    string.Format(s_debugFormat, typeof(KafkaProducerConfig).FullName!, JsonSerializer.Serialize(conf))
                );
                return new KafkaProducer(services, conf);
            });
        }
        return this;
    }
    public IHost Build()
    {
        return _builder.Build();
    }
    private void AddConfig()
    {
        _builder.Services.AddSingleton(services =>
        {
            TConfig conf = JsonSerializer.Deserialize<TConfig>(_json, _jsonSerializerOptions)!;
            services.GetRequiredService<LoggingManager>().Debug(
                string.Format(s_debugFormat, typeof(TConfig).FullName!, JsonSerializer.Serialize(conf))
            );
            return conf;
        });
    }
    private void AddLogging()
    {
        if (
            _json.EnumerateObject().Where(
                e => e.Name.Equals(Constants.LoggingPropertyName, StringComparison.OrdinalIgnoreCase)
            ).Select(e => e.Value).FirstOrDefault() is JsonElement json
            && json.ValueKind is not JsonValueKind.Undefined
        )
        {
            LoggingManager manager = new();
            manager.Configure(_builder.Logging, json, _jsonSerializerOptions);
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
