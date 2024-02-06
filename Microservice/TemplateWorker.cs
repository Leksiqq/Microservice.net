using Net.Leksi.MicroService.Common;
using Net.Leksi.ZkJson;
using org.apache.zookeeper;
using System.Text.Json;

namespace Net.Leksi.MicroService;
public abstract class TemplateWorker<TConfig> : BackgroundService where TConfig : TemplateWorkerConfig, new()
{
    private const string s_missedZkVarPathMessage = "'VarPath' is a mandatory Config property!";
    private const string s_stateFormat = "{{\"state\": \"{0}\", \"time\": \"{1:o}\"}}";
    private const string s_singletonStateFormat = "{{\"{0}\": {{\"state\": \"{1}\", \"time\": \"{2:o}\"}}}}";

    private const string s_stateVarPath = "$state";

    private const int s_minDelay = 100;

    private bool _running = true;
    private bool _isLeader = false;
    private bool _isConfigured = false;

    protected readonly IServiceProvider _services;
    protected readonly ILogger<TemplateWorker<TConfig>>? _logger;
    protected TConfig DefaultConfig { get; private init; } = new()
    {
        PollPeriod = 10000,
        InoperativeDurationWarning = new TimeSpan(0, 0, 20),
        InoperativeDurationError = new TimeSpan(0, 0, 30),
    };
    protected TConfig Config { get; private init; }

    protected bool IsSingleton { get; init; } = false;

    protected abstract bool IsOperative { get; }
    protected DateTime LastOperativeTime { get; private set; } = DateTime.MinValue;
    protected Exception? LastInoperativeException { get; private set; }
    protected CancellationToken StoppingTokenCache { get; private set; } = CancellationToken.None;
    protected string? VarRoot {get; private set; }
    protected ZkJsonSerializer VarSerializer { get; private set; } = null!;
    protected JsonSerializerOptions VarSerializerOptions { get; private set; } = null!;
    protected State State { get; private set; } = State.Idle;
    protected string WorkerId { get; private set; } = null!;
    public string Name { get; private init; }
    public TemplateWorker(IServiceProvider services)
    {
        Console.CancelKeyPress += Console_CancelKeyPress;

        _services = services;
        _logger = (ILogger<TemplateWorker<TConfig>>?)_services.GetService(typeof(ILogger<>).MakeGenericType([ GetType() ]));

        IConfiguration conf = _services.GetRequiredService<IConfiguration>();

        if (bool.TryParse(conf["RUNNING_IN_CONTAINER"], out bool ric) && ric && conf["HOSTNAME"] is { })
        {
            WorkerId = conf["HOSTNAME"]!;
        }
        else
        {
            WorkerId = Guid.NewGuid().ToString();
        }

        Name = conf["name"]!;
        Config = _services.GetRequiredService<TConfig>();
    }
    protected async sealed override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        StoppingTokenCache = stoppingToken;

        while (_running && !stoppingToken.IsCancellationRequested && State is not State.Fail)
        {
            DateTime start = DateTime.UtcNow;
            await CheckConfigured(stoppingToken);
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
                            if ((State is State.Idle || State is State.Operate) && inoperativeTime >= Config!.InoperativeDurationWarning)
                            {
                                State = State.Warning;
                                await ProcessInoperativeWarning(inoperativeTime, LastInoperativeException, stoppingToken);
                            }
                            else if (State is State.Warning && inoperativeTime >= Config!.InoperativeDurationError)
                            {
                                State = State.Error;
                                await ProcessInoperativeError(inoperativeTime, LastInoperativeException, stoppingToken);
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
                            else
                            {
                                State = State.Idle;
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

    private async Task CheckConfigured(CancellationToken stoppingToken)
    {
        if (!_isConfigured)
        {
            try
            {
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
                    ZooKeeper = _services.GetRequiredService<ZooKeeper>(),
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
                    JsonSerializer.Deserialize<ZkStub>("{}", VarSerializerOptions);
                }
                VarSerializer.Reset($"{VarRoot}/{s_stateVarPath}");
                if (!await VarSerializer.RootExists())
                {
                    JsonSerializer.Deserialize<ZkStub>("{}", VarSerializerOptions);
                }

                await BeforeInitializing(stoppingToken);


                if (_logger?.IsEnabled(LogLevel.Information) ?? false)
                {
                    _logger.LogInformation("{config}", JsonSerializer.Serialize(Config));
                }
                await Initialize(stoppingToken);
                LastOperativeTime = DateTime.UtcNow;
                _isConfigured = true;
                UpdateState();
            }
            catch (Exception ex)
            {
                if (_logger?.IsEnabled(LogLevel.Critical) ?? false)
                {
                    _logger.LogCritical("{ex}", ex.ToString());
                }
                _running = false;
            }
        }
    }

    protected abstract Task Exiting(CancellationToken stoppingToken);
    protected abstract Task Operate(CancellationToken stoppingToken);
    protected abstract Task ProcessFail(Exception? lastInoperativeException, CancellationToken stoppingToken);
    protected abstract Task ProcessInoperativeError(TimeSpan inoperativeTime, Exception? lastInoperativeException, CancellationToken stoppingToken);
    protected abstract Task ProcessInoperativeWarning(TimeSpan inoperativeTime, Exception? lastInoperativeException, CancellationToken stoppingToken);
    protected abstract Task MakeOperative(CancellationToken stoppingToken);
    protected abstract Task Initialize(CancellationToken stoppingToken);
    protected abstract Task BeforeInitializing(CancellationToken stoppingToken);
    protected void UpdateState()
    {
        DateTime time = (State is State.Idle || State is State.Operate) ? DateTime.UtcNow : LastOperativeTime;
        if(IsSingleton)
        {
            if (_isLeader)
            {
                VarSerializer.Reset($"{VarRoot}/{s_stateVarPath}");
                JsonSerializer.Deserialize<ZkStub>(
                    string.Format(s_singletonStateFormat, WorkerId, State, time),
                    VarSerializerOptions
                );
            }
        }
        else
        {
            VarSerializer.Reset($"{VarRoot}/{s_stateVarPath}/{WorkerId}");
            JsonSerializer.Deserialize<ZkStub>(
                string.Format(s_stateFormat, State, time),
                VarSerializerOptions
            );
        }
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
    protected bool CanOperate()
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
                        prop.Value.EnumerateObject().Where(e => e.Name == "state").FirstOrDefault().Value is JsonElement je
                        && je.ValueKind is JsonValueKind.String
                        && je.GetString() == State.Operate.ToString()
                        && prop.Value.EnumerateObject().Where(e => e.Name == "time").FirstOrDefault().Value is JsonElement je1 
                        && je1.ValueKind is JsonValueKind.String
                        && je1.GetString() is string ts
                        && DateTime.TryParse(ts, out DateTime time)
                        && DateTime.UtcNow - time.ToUniversalTime() < Config.InoperativeDurationWarning
                    )
                    {
                        if (_isLeader)
                        {
                            if (_logger?.IsEnabled(LogLevel.Information) ?? false)
                            {
                                _logger.LogInformation("{WorkerId} lost leadership!", WorkerId);
                            }
                            _isLeader = false;
                        }
                        return false;
                    }
                }
            }
            if (!_isLeader)
            {
                if (_logger?.IsEnabled(LogLevel.Information) ?? false)
                {
                    _logger.LogInformation("{WorkerId} becomes a leader!", WorkerId);
                }
                _isLeader = true;
            }
        }
        return true;
    }
}
