using MailKit;
using MailKit.Search;
using Net.Leksi.MicroService.Common;
using Net.Leksi.ZkJson;
using System.Text.Json;

namespace Net.Leksi.MicroService.ImapReader;

public class Worker : TemplateWorker<Config>
{
    private const string s_lastUidPath = "lastuid";


    private readonly Client _client = null!;
    private readonly JsonSerializerOptions _mimeJsonSerializerOption = new() { WriteIndented = true, };
    private readonly ICloudClient _storage = null!;
    private readonly IKafkaProducer _kafkaProducer = null!;
    private readonly string _pathPrefix;
    private readonly Random _rnd = new();

    private uint _lastUid = 0;
    private string _lastUidRoot = null!;
    private IMailFolder _folder = null!;
    protected override bool IsOperative => _client.IsConnected && _client.IsAuthenticated;

    public Worker(IServiceProvider services) : base(services) 
    {
        IsSingleton = true;
        DefaultConfig.Folder = "INBOX";
        DefaultConfig.Port = 143;
        _mimeJsonSerializerOption.Converters.Add(new MimeMessageJsonConverter());
        _client = new Client();
        _storage = _services.GetRequiredService<ICloudClient>();
        _kafkaProducer = _services.GetRequiredService<IKafkaProducer>();
        _pathPrefix = Util.CollapseSlashes($"{_storage.Bucket}:{_storage.Folder}/");
    }
    protected override async Task MakeOperative(CancellationToken stoppingToken)
    {
        if (_logger?.IsEnabled(LogLevel.Information) ?? false)
        {
            if (!_client.IsConnected)
            {
                LoggerMessages.ClientReconnecting(_logger, null);
            }
            else if (!_client.IsAuthenticated)
            {
                LoggerMessages.ClientReconnecting(_logger, null);
            }
            else if(_folder is null || !_folder.IsOpen)
            {
                LoggerMessages.ClientReopenningFolder(_logger, null);
            }
        }
        try
        {
            if (!_client.IsConnected)
            {
                await _client.ConnectAsync(Config.Host, Config.Port, false, stoppingToken);
            }
            if (!_client.IsAuthenticated)
            {
                await _client.AuthenticateAsync(Config.Login, Config.Password, stoppingToken);
            }
            if(_folder is null || !_folder.IsOpen)
            {
                _folder = await _client.GetFolderAsync(Config.Folder, stoppingToken);
            }

        }
        finally
        {
            if (_logger?.IsEnabled(LogLevel.Information) ?? false)
            {
                LoggerMessages.ClienConnected(_logger, _client.IsConnected, null);
                LoggerMessages.ClienAuthenticated(_logger, _client.IsAuthenticated, null);
                LoggerMessages.FolderIsOpen(_logger, _folder is { }, null);
            }
        }
    }
    protected override async Task Initialize(CancellationToken cancellationToken)
    {
        _lastUidRoot = $"{VarRoot}/{s_lastUidPath}";

        if (string.IsNullOrEmpty(Config.Host))
        {
            Config.Host = DefaultConfig.Host;
        }
        if (Config.Port == default)
        {
            Config.Port = DefaultConfig.Port;
        }
        if (Config.Folder == default)
        {
            Config.Folder = DefaultConfig.Folder;
        }
        VarSerializer.Reset(_lastUidRoot);
        if (!await VarSerializer.RootExists())
        {
            JsonSerializer.Deserialize<ZkStub>(
                JsonSerializer.SerializeToElement(0), 
                VarSerializerOptions
            );
        }
    }
    protected override async Task ProcessInoperativeWarning(TimeSpan inoperativeTime, Exception? lastInoperativeException, CancellationToken cancellationToken)
    {
        if (_logger?.IsEnabled(LogLevel.Warning) ?? false)
        {
            if (!_client.IsConnected)
            {
                LoggerMessages.ClientNotConnectedWarn(_logger, inoperativeTime, LastInoperativeException);
            }
            if (!_client.IsAuthenticated)
            {
                LoggerMessages.ClientNotAuthenticatedWarn(_logger, inoperativeTime, LastInoperativeException);
            }
        }
        await Task.CompletedTask;
    }
    protected override async Task ProcessInoperativeError(TimeSpan inoperativeTime, Exception? lastInoperativeException, CancellationToken cancellationToken)
    {
        if (_logger?.IsEnabled(LogLevel.Error) ?? false)
        {
            if (!_client.IsConnected)
            {
                LoggerMessages.ClientNotConnectedErr(_logger, inoperativeTime, LastInoperativeException);
            }
            if (!_client.IsAuthenticated)
            {
                LoggerMessages.ClientNotAuthenticatedErr(_logger, inoperativeTime, LastInoperativeException);
            }
        }
        await Task.CompletedTask;
    }
    protected override async Task Operate(CancellationToken stoppingToken)
    {
        RefreshLastUid();

        await _folder.OpenAsync(FolderAccess.ReadOnly, stoppingToken);

        List<UniqueId> uids = Range(_lastUid + 1, _folder.UidNext!.Value.Id).ToList();

        if(uids.Count > 0)
        {
            foreach (var item in await _folder.SearchAsync(SearchQuery.Uids(uids), stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested || !CanOperate())
                {
                    break;
                }
                _lastUid = item.Id;
                if (_services.GetRequiredService<IConfiguration>() is IConfiguration conf && conf["break"] is { } && _lastUid >= 8456)
                {
                    throw new InvalidOperationException();
                }
                if (_logger?.IsEnabled(LogLevel.Information) ?? false)
                {
                    LoggerMessages.MessageReceived(_logger, item.Id, null);
                }

                long ticks;
                string name;

                while (true)
                {
                    ticks = DateTime.UtcNow.Ticks;
                    name = $"{ticks}-{string.Join(string.Empty, Enumerable.Range(0, 5).Select(i => (char)_rnd.Next('a', 'z' + 1)).ToArray())}";
                    if(await _storage.FileExists(name, stoppingToken))
                    {
                        break;
                    }
                }

                MemoryStream ms = new();
                await JsonSerializer.SerializeAsync(ms, await _folder.GetMessageAsync(item, stoppingToken), _mimeJsonSerializerOption, stoppingToken);
                ms.Flush();
                ms.Position = 0;

                string path = $"{_pathPrefix}{name}";

                await _storage.UploadFile(ms, path, "application/json", ms.Length, stoppingToken);

                await _kafkaProducer.ProduceAsync(
                    new ReceivedFileKafkaMessage { Path = path, }, 
                    stoppingToken
                );

                SaveLastUid();
                UpdateState();
            }
        }

        await _folder.CloseAsync(false, stoppingToken);
    }
    protected override async Task Exiting(CancellationToken stoppingToken)
    {
        _kafkaProducer?.Dispose();
        _storage?.Dispose();
        _client?.Dispose();
        await Task.CompletedTask;
    }
    private static IEnumerable<UniqueId> Range(uint start, uint finish)
    {
        for(uint id = start; id < finish; ++id) 
        {
            yield return new UniqueId(id);
        }
    }
    private void RefreshLastUid()
    {
        VarSerializer.Reset(_lastUidRoot);
        _lastUid = JsonSerializer.Deserialize<uint>(JsonSerializer.SerializeToElement(ZkStub.Instance, VarSerializerOptions));
    }
    private void SaveLastUid()
    {
        VarSerializer.Reset(_lastUidRoot);
        JsonSerializer.Deserialize<ZkStub>(_lastUid.ToString(), VarSerializerOptions);
    }
}
