using Net.Leksi.MicroService.Common;
using Net.Leksi.ZkJson;
using org.apache.zookeeper;
using System.Text;
using System.Text.Json;

namespace Net.Leksi.MicroService;
public abstract class TemplateWorker<TConfig> : BackgroundService where TConfig : TemplateConfig, new()
{
    private class ZkWatchwer(TemplateWorker<TConfig> worker) : Watcher
    {
        public override async Task process(WatchedEvent evt)
        {
            if (worker._logger is { } && worker._logger.IsEnabled(LogLevel.Information))
            {
                worker._logger.LogInformation("{evt}: {path}", evt, evt.getPath());
            }
            if (!worker._mres.IsSet)
            {
                worker._mres.Set();
            }
            await Task.CompletedTask;
        }
    }

    private const string s_missedZkMessage = "ZooKeeper address(es) '--zkAddr <address(es)>' is a mandatory command line parameter!";
    private const string s_missedNameMessage = "Service name '--name <service name>' is a mandatory command line parameter!";
    private const string s_zkAddrMessage = "--zkAddr: {zk}";
    private const string s_configMessage = "--config: {conf}";
    private const string s_nameMessage = "--name: {name}";
    private const string s_zkConnTimeoutMessage = "--zkConnTimeout: {to}";
    private const string s_zkConnectedMessage = "ZooKeeper connected";
    private const string s_zkErrorMessage = "Zookeeper error: {ex}";
    private const string s_missedZkVarPathMessage = "'VarPath' is a mandatory Config property!";
    private const string s_stateFormat = "{{\"state\": \"{0}\", \"time\": \"{1:o}\"}}";

    private const string s_stateVarPath = "$state";

    private const int s_minDelay = 100;
    private const int s_defaultZkConnTimeout = 10000;

    private readonly string _zkAddr;
    private readonly string _zkConfigPath;
    private readonly int _zkConnTimeout;
    private readonly ManualResetEventSlim _mres = new(false);
    private readonly JsonSerializerOptions configDeserializationOption = new() { PropertyNameCaseInsensitive = true, };
    private readonly JsonSerializerOptions configSerializationOption = new();

    private bool _running = true;
    private bool _isConfigured = false;
    private bool _isLeader = false;

    protected readonly IServiceProvider _services;
    protected string Name { get; private init; }
    protected TConfig Config { get; private set; }

    protected readonly ILogger<TemplateWorker<TConfig>>? _logger;
    protected TConfig DefaultConfig { get; private init; } = new()
    {
        PollPeriod = 10000,
        InoperativeDurationWarning = new TimeSpan(0, 0, 20),
        InoperativeDurationError = new TimeSpan(0, 0, 30),
    };
    protected bool IsSingleton { get; init; } = false;

    protected abstract bool IsOperative { get; }
    protected DateTime LastOperativeTime { get; private set; } = DateTime.MinValue;
    protected Exception? LastInoperativeException { get; private set; }
    protected ZooKeeper? ZooKeeper { get; private init; }
    protected CancellationToken StoppingTokenCache { get; private set; } = CancellationToken.None;
    protected string? VarRoot {get; private set; }
    protected ZkJsonSerializer VarSerializer { get; private set; } = null!;
    protected JsonSerializerOptions VarSerializerOptions { get; private set; } = null!;
    protected State State { get; private set; } = State.Idle;
    protected string WorkerId { get; private set; } = null!;



    public TemplateWorker(IServiceProvider services)
    {
        Console.CancelKeyPress += Console_CancelKeyPress;

        _services = services;
        _logger = (ILogger<TemplateWorker<TConfig>>?)_services.GetService(typeof(ILogger<>).MakeGenericType([ GetType() ]));
        Config = DefaultConfig;

        IConfiguration conf = _services.GetRequiredService<IConfiguration>();

        if (bool.TryParse(conf["RUNNING_IN_CONTAINER"], out bool ric) && ric && conf["HOSTNAME"] is { })
        {
            WorkerId = conf["HOSTNAME"]!;
        }
        else
        {
            WorkerId = Guid.NewGuid().ToString();
        }

        Name = conf["name"] ?? string.Empty;
        _zkAddr = conf["zkAddr"] ?? string.Empty;
        _zkConfigPath = conf["config"] ?? "/";

        if (!int.TryParse(conf["zkConnTimeout"], out _zkConnTimeout))
        {
            _zkConnTimeout = s_defaultZkConnTimeout;
        }

        if (_logger is { })
        {
            if (_logger.IsEnabled(LogLevel.Critical))
            {
                if (string.IsNullOrEmpty(_zkAddr))
                {
                    _logger.LogCritical(s_missedZkMessage);
                }
                if (string.IsNullOrEmpty(Name))
                {
                    _logger.LogCritical(s_missedNameMessage);
                }
            }
        }
        if (string.IsNullOrEmpty(_zkAddr) || string.IsNullOrEmpty(Name))
        {
            _running = false;
            return;
        }
        if (_logger is { } && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(s_zkAddrMessage, _zkAddr);
            _logger.LogInformation(s_zkConnTimeoutMessage, _zkConnTimeout);
            _logger.LogInformation(s_configMessage, _zkConfigPath);
        }
        ZooKeeper = new ZooKeeper(_zkAddr, _zkConnTimeout, new ZkWatchwer(this));
    }
    protected async sealed override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        StoppingTokenCache = stoppingToken;
        _mres.Wait(_zkConnTimeout, stoppingToken);
        if (ZooKeeper is null || ZooKeeper.getState() is not ZooKeeper.States.CONNECTED)
        {
            _running = false;
        }
        while (_running && !stoppingToken.IsCancellationRequested && State is not State.Fail)
        {
            DateTime start = DateTime.UtcNow;
            if (!_isConfigured && ZooKeeper is { } && ZooKeeper.getState() is ZooKeeper.States.CONNECTED)
            {
                if (_logger is { } && _logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(s_zkConnectedMessage);
                }
                try
                {
                    ZkJsonSerializer configSerializer = new()
                    {
                        ZooKeeper = ZooKeeper,
                        Root = $"{_zkConfigPath}/{Name}",
                    };
                    configSerializationOption.Converters.Add(configSerializer);

                    Config = JsonSerializer.Deserialize<TConfig>(
                        configSerializer.IncrementalSerialize("$base"),
                        configDeserializationOption
                    )!;

                    JsonSerializer.Serialize(Console.OpenStandardOutput(), Config);

                    if (Config.PollPeriod == default)
                    {
                        Config.PollPeriod = DefaultConfig.PollPeriod;
                    }
                    if (Config.InoperativeDurationWarning == default)
                    {
                        Config.InoperativeDurationWarning = DefaultConfig.InoperativeDurationWarning;
                    }
                    if (Config.InoperativeDurationError == default)
                    {
                        Config.InoperativeDurationError = DefaultConfig.InoperativeDurationError;
                    }

                    if (string.IsNullOrEmpty(Config.VarPath))
                    {
                        throw new InvalidOperationException(s_missedZkVarPathMessage);
                    }

                    VarRoot = $"{Config.VarPath}/{Name}";
                    VarSerializerOptions = new();
                    VarSerializer = new ZkJsonSerializer()
                    {
                        ZooKeeper = ZooKeeper!,
                        Root = Config.VarPath,
                    };
                    VarSerializerOptions.Converters.Add(VarSerializer);

                    if (!await VarSerializer.RootExists())
                    {
                        await VarSerializer.CreateRoot();
                    }
                    VarSerializer.Reset(VarRoot);
                    if (!await VarSerializer.RootExists())
                    {
                        string json = "{}";
                        JsonSerializer.Deserialize<ZkStub>(
                            new MemoryStream(
                                Encoding.ASCII.GetBytes(json)
                            ),
                            VarSerializerOptions
                        );
                    }
                    VarSerializer.Reset($"{VarRoot}/{s_stateVarPath}");
                    if (!await VarSerializer.RootExists())
                    {
                        string json = "{}";
                        JsonSerializer.Deserialize<ZkStub>(
                            new MemoryStream(
                                Encoding.ASCII.GetBytes(json)
                            ),
                            VarSerializerOptions
                        );
                    }

                    await BeforeInitializing(stoppingToken);


                    if (_logger is { } && _logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("{config}", JsonSerializer.Serialize(Config));
                    }
                    await Initialize(stoppingToken);
                    LastOperativeTime = DateTime.UtcNow;
                    _isConfigured = true;
                    PublishState();
                }
                catch (Exception ex)
                {
                    if (_logger is { } && _logger.IsEnabled(LogLevel.Critical))
                    {
                        _logger.LogCritical(s_zkErrorMessage, ex);
                    }
                    _running = false;
                }
            }
            if (_running)
            {
                if (_isConfigured)
                {
                    if (!IsOperative)
                    {
                        try
                        {
                            await MakeOperative(stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            State = State.Fail;
                            LastInoperativeException = ex;
                            await ProcessFail(LastInoperativeException, stoppingToken);
                        }
                    }
                    if (!IsOperative)
                    {
                        TimeSpan inoperativeTime = DateTime.UtcNow - LastOperativeTime;
                        if (State is not State.Fail)
                        {
                            if (inoperativeTime >= Config!.InoperativeDurationWarning)
                            {
                                State = State.Warning;
                                await ProcessInoperativeWarning(inoperativeTime, LastInoperativeException, stoppingToken);
                                if (inoperativeTime >= Config!.InoperativeDurationError)
                                {
                                    State = State.Error;
                                    await ProcessInoperativeError(inoperativeTime, LastInoperativeException, stoppingToken);
                                }
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            LastOperativeTime = DateTime.UtcNow;
                            if (CanOperate())
                            {
                                State = State.Operate;
                                UpdateState();
                                await Operate(stoppingToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            State = State.Fail;
                            LastInoperativeException = ex;
                            UpdateState();
                            await ProcessFail(LastInoperativeException, stoppingToken);
                        }
                    }

                }
                int msLeft = Config!.PollPeriod - (int)(DateTime.UtcNow - start).TotalMilliseconds;
                try
                {
                    await Task.Delay(msLeft >= s_minDelay ? msLeft : s_minDelay, stoppingToken);
                }
                catch (TaskCanceledException) { }
            }
        }
        await DeleteState();
        await _services.GetRequiredService<IHost>().StopAsync(stoppingToken);
    }
    protected abstract Task Exiting(CancellationToken stoppingToken);
    protected abstract Task Operate(CancellationToken stoppingToken);
    protected abstract Task ProcessFail(Exception? lastInoperativeException, CancellationToken stoppingToken);
    protected abstract Task ProcessInoperativeError(TimeSpan inoperativeTime, Exception? lastInoperativeException, CancellationToken stoppingToken);
    protected abstract Task ProcessInoperativeWarning(TimeSpan inoperativeTime, Exception? lastInoperativeException, CancellationToken stoppingToken);
    protected abstract Task MakeOperative(CancellationToken stoppingToken);
    protected abstract Task Initialize(CancellationToken stoppingToken);
    protected abstract Task BeforeInitializing(CancellationToken stoppingToken);
    protected void PublishState()
    {
        VarSerializer.Reset($"{VarRoot}/{s_stateVarPath}/{WorkerId}");
        JsonSerializer.Deserialize<ZkStub>(
            new MemoryStream(
                Encoding.ASCII.GetBytes(string.Format(s_stateFormat, State, LastOperativeTime))
            ),
            VarSerializerOptions
        );
    }
    protected void UpdateState()
    {
        DateTime time = (State is State.Idle || State is State.Operate) ? DateTime.UtcNow : LastOperativeTime;
        VarSerializer.Reset($"{VarRoot}/{s_stateVarPath}/{WorkerId}");
        ZkAction saveAction = VarSerializer.Action;
        if(!IsSingleton)
        {
            VarSerializer.Action = ZkAction.Update;
        }
        JsonSerializer.Deserialize<ZkStub>(
            new MemoryStream(
                Encoding.ASCII.GetBytes(string.Format(s_stateFormat, State, time))
            ),
            VarSerializerOptions
        );
        VarSerializer.Action = saveAction;
    }
    private async Task DeleteState()
    {
        VarSerializer.Reset($"{VarRoot}/{s_stateVarPath}/{WorkerId}");
        await VarSerializer.DeleteAsync();
    }
    private void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        StopAsync(StoppingTokenCache).Wait();
    }
    private bool CanOperate()
    {
        if (IsSingleton)
        {
            VarSerializer.Reset($"{VarRoot}/{s_stateVarPath}");
            JsonElement states = JsonSerializer.SerializeToElement(ZkStub.Instance, VarSerializerOptions);
            foreach (JsonProperty prop in states.EnumerateObject())
            {
                if(prop.Name != WorkerId)
                {
                    if(
                        prop.Value.EnumerateObject().Where(e => e.Name == "time").FirstOrDefault().Value is JsonElement je 
                        && je.ValueKind is JsonValueKind.String
                        && je.GetString() is string ts
                        && DateTime.TryParse(ts, out DateTime time)
                        && DateTime.UtcNow - time < Config.InoperativeDurationWarning
                    )
                    {
                        return false;
                    }
                }
            }
            if (!_isLeader)
            {
                if (_logger is { } && _logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation($"{WorkerId} becomes a leader!");
                }
            }
            _isLeader = true;
        }
        return true;
    }
}
