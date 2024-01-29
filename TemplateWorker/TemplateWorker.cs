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
    private const string s_singletonWithotVarPathMessage = $"The running worker is marked as 'Singleton', but 'VarPath' is not set!";

    private const string s_singletonVarPath = "$singleton";

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
    protected ZkJsonSerializer? VarSerializer {get; private set;}
    protected JsonSerializerOptions? VarSerializerOptions { get; private set; }



    public TemplateWorker(IServiceProvider services)
    {
        Console.CancelKeyPress += Console_CancelKeyPress;

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
                    ZkJsonSerializer configSerializer = new()
                    {
                        ZooKeeper = ZooKeeper,
                        Root = $"{_zkConfigPath}/{Name}",
                    };
                    configSerializationOption.Converters.Add(configSerializer);
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

                    if (IsSingleton && Config.VarPath is null)
                    {
                        throw new InvalidOperationException(s_singletonWithotVarPathMessage);
                    }

                    if (Config.VarPath is { })
                    {
                        VarRoot = $"{Config.VarPath}/{Name}";
                        VarSerializerOptions = new();
                        VarSerializer = new ZkJsonSerializer()
                        {
                            ZooKeeper = ZooKeeper!,
                            Root = Config.VarPath,
                        };
                        if (!await VarSerializer.RootExists())
                        {
                            await VarSerializer.CreateRoot();
                        }
                        VarSerializer.Root = VarRoot;
                        VarSerializerOptions.Converters.Add(VarSerializer);
                    }

                    await BeforeInitializing(stoppingToken);


                    if (_logger is { } && _logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("{config}", JsonSerializer.Serialize(Config));
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
                        await ProcessInoperativeWarning(inoperativeTime, LastInoperativeException, stoppingToken);
                        if (inoperativeTime >= Config!.InoperativeDurationError)
                        {
                            await ProcessInoperativeError(inoperativeTime, LastInoperativeException, stoppingToken);
                        }
                    }
                }
                else
                {
                    try
                    {
                        if (await CanOperate(stoppingToken))
                        {
                            await Operate(stoppingToken);
                        }
                        LastOperativeTime = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        LastInoperativeException = ex;
                    }
                }

            }
            if (_running)
            {
                int msLeft = Config!.PollPeriod - (int)(DateTime.UtcNow - start).TotalMilliseconds;
                await Task.Delay(msLeft >= s_minDelay ? msLeft : s_minDelay, stoppingToken);
            }
        }
        await Exiting(stoppingToken);
        await _services.GetRequiredService<IHost>().StopAsync(stoppingToken);
    }
    protected abstract Task Exiting(CancellationToken stoppingToken);
    protected abstract Task Operate(CancellationToken stoppingToken);
    protected abstract Task ProcessInoperativeError(TimeSpan inoperativeTime, Exception? lastInoperativeException, CancellationToken stoppingToken);
    protected abstract Task ProcessInoperativeWarning(TimeSpan inoperativeTime, Exception? lastInoperativeException, CancellationToken stoppingToken);
    protected abstract Task MakeOperative(CancellationToken stoppingToken);
    protected abstract Task Initialize(CancellationToken stoppingToken);
    protected abstract Task BeforeInitializing(CancellationToken stoppingToken);
    private void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        StopAsync(StoppingTokenCache).Wait();
    }
    private async Task<bool> CanOperate(CancellationToken stoppingToken)
    {
        if (IsSingleton)
        {
            VarSerializer!.Reset($"{VarRoot}/{s_singletonVarPath}");
            if (! await VarSerializer.RootExists())
            {

            }
        }
        return true;
    }
}
