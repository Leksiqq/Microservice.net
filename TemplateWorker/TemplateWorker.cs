using Net.Leksi.MicroService.Common;
using Net.Leksi.ZkJson;
using org.apache.zookeeper;
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

    private const int s_minDelay = 100;
    private const int s_defaultZkConnTimeout = 10000;

    private readonly IServiceProvider _services;
    private readonly string _zkAddr;
    private readonly string _zkConfigPath;
    private readonly int _zkConnTimeout;
    private readonly ManualResetEventSlim _mres = new(false);
    private readonly JsonSerializerOptions configDeserializationOption = new() { PropertyNameCaseInsensitive = true, };
    private readonly JsonSerializerOptions configSerializationOption = new();

    private bool _running = true;
    private bool _isConfigured = false;

    protected string Name { get; private init; }
    protected TConfig Config { get; private set; }

    protected readonly ILogger<TemplateWorker<TConfig>>? _logger;
    protected TConfig DefaultConfig { get; private init; } = new()
    {
        PollPeriod = 10000,
        InoperativeDurationWarning = new TimeSpan(0, 0, 20),
        InoperativeDurationError = new TimeSpan(0, 0, 30),
    };

    protected abstract bool IsOperative { get; }
    protected DateTime LastOperativeTime { get; private set; } = DateTime.MinValue;
    protected Exception? LastInoperativeException { get; private set; }
    protected ZooKeeper? ZooKeeper { get; private init; }
    protected CancellationToken StoppingTokenCache { get; private set; } = CancellationToken.None;


    public TemplateWorker(IServiceProvider services)
    {
        _services = services;
        _logger = (ILogger<TemplateWorker<TConfig>>?)_services.GetService(typeof(ILogger<>).MakeGenericType([ GetType() ]));
        Config = DefaultConfig;

        IConfiguration conf = _services.GetRequiredService<IConfiguration>();

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
        while (_running && !stoppingToken.IsCancellationRequested)
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
                    ZkJsonSerializer zkJson = new()
                    {
                        ZooKeeper = ZooKeeper,
                        Root = $"{_zkConfigPath}/{Name}",
                    };
                    configSerializationOption.Converters.Add(zkJson);
                    Config = JsonSerializer.Deserialize<TConfig>(
                        JsonSerializer.SerializeToElement(ZkStub.Instance, configSerializationOption),
                        configDeserializationOption
                    )!;

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

                    FillMissedOptions(DefaultConfig, Config);

                    if (_logger is { } && _logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation(JsonSerializer.Serialize(Config));
                    }
                    await Initialize(stoppingToken);
                    LastOperativeTime = DateTime.UtcNow;
                    _isConfigured = true;
                }
                catch (Exception ex)
                {
                    if (_logger is { } && _logger.IsEnabled(LogLevel.Critical))
                    {
                        _logger.LogCritical(s_zkErrorMessage, ex.Message);
                    }
                    _running = false;
                }
            }
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
                        LastInoperativeException = ex;
                    }
                }
                if (!IsOperative)
                {
                    TimeSpan inoperativeTime = DateTime.UtcNow - LastOperativeTime;
                    if (inoperativeTime >= Config!.InoperativeDurationWarning)
                    {
                        ProcessInoperativeWarning(inoperativeTime, LastInoperativeException);
                        if (inoperativeTime >= Config!.InoperativeDurationError)
                        {
                            ProcessInoperativeError(inoperativeTime, LastInoperativeException);
                        }
                    }
                }
                else
                {
                    await Operate(stoppingToken);
                    LastOperativeTime = DateTime.UtcNow;
                }

            }
            int msLeft = Config!.PollPeriod - (int)(DateTime.UtcNow - start).TotalMilliseconds;
            await Task.Delay(msLeft >= s_minDelay ? msLeft : s_minDelay, stoppingToken);
        }
        await _services.GetRequiredService<IHost>().StopAsync(stoppingToken);
    }

    protected abstract Task Operate(CancellationToken stoppingToken);
    protected abstract void ProcessInoperativeError(TimeSpan inoperativeTime, Exception? lastInoperativeException);
    protected abstract void ProcessInoperativeWarning(TimeSpan inoperativeTime, Exception? lastInoperativeException);
    protected abstract Task MakeOperative(CancellationToken stoppingToken);
    protected abstract Task Initialize(CancellationToken stoppingToken);
    protected abstract void FillMissedOptions(TConfig defaultConfig, TConfig config);
}
