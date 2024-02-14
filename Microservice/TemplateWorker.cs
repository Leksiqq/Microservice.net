using Net.Leksi.MicroService.Common;
using Net.Leksi.ZkJson;
using org.apache.zookeeper;
using System.Reflection;
using System.Text.Json;

namespace Net.Leksi.MicroService;
public abstract class TemplateWorker<TConfig> : BackgroundService where TConfig : TemplateWorkerConfig, new()
{
    private const string s_stateFormat = "{{\"state\": \"{0}\", \"time\": \"{1:o}\"}}";
    private const string s_singletonStateFormat = "{{\"{0}\": {{\"state\": \"{1}\", \"time\": \"{2:o}\"}}}}";

    private const string s_stateVarPath = "$state";

    private const int s_minDelay = 100;

    private bool _running = true;
    private bool _isLeader = false;
    private bool _isConfigured = false;

    protected readonly IServiceProvider _services;
    protected readonly ILogger<TemplateWorker<TConfig>>? _logger;
    protected readonly WorkerId _workerId;
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
    public TemplateWorker(IServiceProvider services)
    {
        Console.CancelKeyPress += Console_CancelKeyPress;

        _services = services;
        _logger = (ILogger<TemplateWorker<TConfig>>)_services.GetRequiredService(
            typeof(ILogger<>).MakeGenericType([GetType()])
        );
        _workerId = _services.GetRequiredService<WorkerId>();

        Config = _services.GetRequiredService<TConfig>();

        if(Config.Name is { })
        {
            _workerId.Name = Config.Name;
        }
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
                int msLeft = (Config!.PollPeriod ?? 0) - (int)(DateTime.UtcNow - start).TotalMilliseconds;
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
                foreach(PropertyInfo pi in typeof(TConfig).GetProperties())
                {
                    if (
                        pi.GetValue(Config) is null
                        && pi.GetValue(DefaultConfig) is not null 
                    )
                    {
                        Console.WriteLine($"set: {pi} = {pi.GetValue(DefaultConfig)}");
                        pi.SetValue(Config, pi.GetValue(DefaultConfig));
                    }
                }

                if (string.IsNullOrEmpty(Config.VarPath))
                {
                    throw new ArgumentException(string.Format(Constants.MissedMandatoryProperty, nameof(Config), nameof(Config.VarPath)));
                }

                VarRoot = $"{Config.VarPath}/{_workerId.Name}";
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

                await Initialize(stoppingToken);
                _services.GetRequiredService<LoggingManager>().Debug(
                    string.Format("{0}: {1}", typeof(TConfig).FullName!, JsonSerializer.Serialize(Config))
                );

                LastOperativeTime = DateTime.UtcNow;
                _isConfigured = true;
                UpdateState();
            }
            catch (Exception ex)
            {
                if (_logger?.IsEnabled(LogLevel.Critical) ?? false)
                {
                    Common.LoggerMessages.Exception(_logger, ex.Message, ex.StackTrace!, ex);
                }
                _running = false;
            }
        }
    }

    protected abstract Task Exiting(CancellationToken stoppingToken);
    protected abstract Task Operate(CancellationToken stoppingToken);
    protected virtual async Task ProcessFail(Exception? lastInoperativeException, CancellationToken stoppingToken)
    {
        if (_logger?.IsEnabled(LogLevel.Critical) ?? false)
        {
            Common.LoggerMessages.Exception(
                _logger,
                LastInoperativeException!.Message,
                lastInoperativeException!.StackTrace!,
                LastInoperativeException
                );
        }
        await Task.CompletedTask;
    }
    protected abstract Task ProcessInoperativeError(TimeSpan inoperativeTime, Exception? lastInoperativeException, CancellationToken stoppingToken);
    protected abstract Task ProcessInoperativeWarning(TimeSpan inoperativeTime, Exception? lastInoperativeException, CancellationToken stoppingToken);
    protected abstract Task MakeOperative(CancellationToken stoppingToken);
    protected abstract Task Initialize(CancellationToken stoppingToken);
    protected void UpdateState()
    {
        DateTime time = (State is State.Idle || State is State.Operate) ? DateTime.UtcNow : LastOperativeTime;
        if(IsSingleton)
        {
            if (_isLeader)
            {
                VarSerializer.Reset($"{VarRoot}/{s_stateVarPath}");
                JsonSerializer.Deserialize<ZkStub>(
                    string.Format(s_singletonStateFormat, _workerId.Id, State, time),
                    VarSerializerOptions
                );
            }
        }
        else
        {
            VarSerializer.Reset($"{VarRoot}/{s_stateVarPath}/{_workerId.Id}");
            JsonSerializer.Deserialize<ZkStub>(
                string.Format(s_stateFormat, State, time),
                VarSerializerOptions
            );
        }
    }
    private async Task DeleteState()
    {
        VarSerializer.Reset($"{VarRoot}/{s_stateVarPath}/{_workerId.Id}");
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
                if(prop.Name != _workerId.Id)
                {
                    if (
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
                                Logging.LoggerMessages.LostLeadership(_logger, _workerId.Id, null);
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
                    Logging.LoggerMessages.BecomeLeader(_logger, _workerId.Id, null);
                }
                _isLeader = true;
            }
        }
        return true;
    }
}
