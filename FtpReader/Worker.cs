using FluentFTP;
using Net.Leksi.MicroService.Common;
using Net.Leksi.ZkJson;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Net.Leksi.MicroService.FtpReader;
public partial class Worker : TemplateWorker<Config>
{
    private const string s_lastTimePath = "lasttime";
    private const string s_lastNamePath = "lastname";
    private const string s_applicationOctetStream = "application/octet-stream";
    private class FtpLogger(ILogger<TemplateWorker<Config>> logger) : IFtpLogger
    {
        public void Log(FtpLogEntry entry)
        {
            switch(entry.Severity)
            {
                case FtpTraceLevel.Info:
                    LoggerMessages.FtpClientInfo(logger, entry.Message, null);
                    break;
                case FtpTraceLevel.Warn:
                    LoggerMessages.FtpClientWarn(logger, entry.Message, null);
                    break;
                case FtpTraceLevel.Error:
                    LoggerMessages.FtpClientErr(logger, entry.Message, null);
                    break;
            };
        }
    }
    private readonly ICloudClient _storage = null!;
    private readonly KafkaProducer _kafkaProducer = null!;
    private readonly AsyncFtpClient _client;
    private readonly PriorityQueue<FileStat, object> _queue = new();
    private readonly Dictionary<string, FileStat> _dict = [];

    private bool _folderIsOpen = false;
    private Regex? _pattern = null;
    private long _lastTime = 0;
    private string _lastName = string.Empty;
    private string _lastTimeRoot = null!;
    private string _lastNameRoot = null!;
    protected override bool IsOperative => _client.IsConnected && _client.IsAuthenticated && _folderIsOpen;
    public Worker(IServiceProvider services) : base(services)
    {
        IsSingleton = true;
        DefaultConfig.Login = "anonymous";
        DefaultConfig.Port = 21;
        DefaultConfig.FullTimeListing = true;
        DefaultConfig.SizeChangeTimeout = 10000;
        DefaultConfig.ListingSort = ListingSort.Created;

        _client = new AsyncFtpClient();
        _storage = _services.GetRequiredKeyedService<ICloudClient>("storage");
        _kafkaProducer = _services.GetRequiredKeyedService<KafkaProducer>("kafka");
    }
    public Worker WithCustomListingParser(FtpConfig.CustomParser customListingParser)
    {
        Config.ListingParser = customListingParser;
        return this;
    }
    protected override async Task Exiting(CancellationToken stoppingToken)
    {
        _storage?.Dispose();
        _kafkaProducer?.Dispose();
        _client?.Dispose();
        await Task.CompletedTask;
    }
    protected override async Task Initialize(CancellationToken stoppingToken)
    {
        _client.Host = Config.Host;
        _client.Port = Config.Port ?? 0;
        _client.Credentials = new NetworkCredential(Config.Login, Config.Password);
        if (Config.LogClient is bool b && b)
        {
            _client.Logger = new FtpLogger(_logger!);
        }
        if (string.IsNullOrEmpty(Config.Folder))
        {
            _folderIsOpen = true;
        }
        if (!string.IsNullOrEmpty(Config.Encoding))
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _client.Encoding = Encoding.GetEncoding(Config.Encoding);
        }
        if (!string.IsNullOrEmpty(Config.Pattern))
        {
            _pattern = new Regex(Config.Pattern);
        }
        if ((Config.FullTimeListing is null || (Config.FullTimeListing is bool b1 && !b1)) && Config.ListingParser is { })
        {
            _client.Config.ListingCustomParser = Config.ListingParser;
        }
        _client.Config.TimeConversion = FtpDate.UTC;

        _lastTimeRoot = $"{VarRoot}/{s_lastTimePath}";
        _lastNameRoot = $"{VarRoot}/{s_lastNamePath}";

        if(Config.ListingSort is ListingSort.Created)
        {
            VarSerializer.Reset(_lastNameRoot);
            await VarSerializer.DeleteAsync();
            VarSerializer.Reset(_lastTimeRoot);
            if (!await VarSerializer.RootExists())
            {
                JsonSerializer.Deserialize<ZkStub>(
                    JsonSerializer.SerializeToElement(_lastTime),
                    VarSerializerOptions
                );
            }
        }
        else
        {
            VarSerializer.Reset(_lastTimeRoot);
            await VarSerializer.DeleteAsync();
            VarSerializer.Reset(_lastNameRoot);
            if (!await VarSerializer.RootExists())
            {
                JsonSerializer.Deserialize<ZkStub>(
                    JsonSerializer.SerializeToElement(_lastName),
                    VarSerializerOptions
                );
            }
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
    protected override async Task MakeOperative(CancellationToken stoppingToken)
    {
        if (_logger?.IsEnabled(LogLevel.Information) ?? false)
        {
            if (!_client.IsConnected)
            {
                LoggerMessages.ClientReconnecting(_logger, null);
            }
            else if(!_folderIsOpen)
            {
                LoggerMessages.ClientReopenningFolder(_logger, null);
            }
        }
        try
        {
            if (!_client.IsConnected)
            {
                await _client.Connect(true, stoppingToken);
            }
            if (!_folderIsOpen)
            {
                await _client.SetWorkingDirectory(Config.Folder, stoppingToken);
                _folderIsOpen = await _client.GetWorkingDirectory(stoppingToken) == Config.Folder;
            }

        }
        finally
        {
            if (_logger?.IsEnabled(LogLevel.Information) ?? false)
            {
                LoggerMessages.ClientConnected(_logger, _client.IsConnected, null);
                LoggerMessages.ClientAuthenticated(_logger, _client.IsAuthenticated, null);
                LoggerMessages.FolderIsOpen(_logger, _folderIsOpen, null);
            }
        }
    }
    protected override async Task Operate(CancellationToken stoppingToken)
    {
        RefreshLast();
        int count = 0;
        await foreach (FtpListItem item in GetListing(stoppingToken))
        {
            if (stoppingToken.IsCancellationRequested || !CanOperate())
            {
                break;
            }
            if (item.Type is FtpObjectType.File && (_pattern?.IsMatch(item.Name) ?? true) && IsNew(item))
            {
                if(_dict.TryGetValue(item.FullName, out FileStat? stat))
                {
                    if(item.Size != stat.Size)
                    {
                        stat.ChangedSize = DateTime.UtcNow;
                        stat.Size = item.Size;
                    }
                }
                else
                {
                    stat = new FileStat 
                    { 
                        Created = item.Created,
                        Size = item.Size,
                        ChangedSize = DateTime.UtcNow,
                        FullName = item.FullName,
                        Name = item.Name,
                    };
                    _dict.Add(item.FullName, stat);
                    _queue.Enqueue(stat, Config.ListingSort is ListingSort.Created ? stat.Created : stat.Name);
                }
                ++count;
            }
        }

        DateTime now = DateTime.UtcNow;
        while (_queue.Count > 0)
        {
            FileStat stat = _queue.Peek();
            if((now - stat.ChangedSize).TotalMilliseconds >= Config.SizeChangeTimeout)
            {
                StoredFolder storedFolder = _services.GetRequiredService<StoredFolder>();
                StoredFile storedFile = new();
                storedFolder.Files.Add(storedFile);

                MemoryStream ms = new();

                await _client.DownloadStream(ms, stat.FullName, token: stoppingToken);

                ms.Flush();
                ms.Position = 0;

                storedFile.Path = await _storage.UploadFile(ms, stat.Name, s_applicationOctetStream, ms.Length, stoppingToken);

                await _kafkaProducer.ProduceAsync(
                    new ReceivedFilesKafkaMessage { StoredFolder = storedFolder, },
                    stoppingToken
                );

                if (Config.DeleteAfterDownload is bool b && b)
                {
                    await _client.DeleteFile(stat.FullName, stoppingToken);
                }

                _queue.Dequeue();
                _dict.Remove(stat.FullName);
                _lastTime = stat.Created.Ticks;
                _lastName = stat.Name;
                SaveLast();
                UpdateState();
            }
            else
            {
                break;
            }
        }
    }
    [GeneratedRegex(@"^(?<permissions>(?<d>[dl-])(?<op>(?<or>[r-])(?<ow>[w-])(?<ox>[x-]))(?<gp>(?<gr>[r-])(?<gw>[w-])(?<gx>[x-]))(?<ap>(?<ar>[r-])(?<aw>[w-])(?<ax>[x-])))\s+\d+\s+(?<user>.+?)\s+(?<group>.+?)\s+(?<size>\d+)\s+(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{9}\s+[+-]\d{4})\s+(?<name>[^.].*?)(?<link>\s+->\s(?<src>.+))?$")]
    private static partial Regex FullTimeListingPattern();
    private bool IsNew(FtpListItem item)
    {
        if (Config.ListingSort is ListingSort.Created)
        {
            return item.Created.Ticks > _lastTime;
        }
        return item.Name.CompareTo(_lastName) > 0;
    }

    private void SaveLast()
    {
        if(Config.ListingSort is ListingSort.Created)
        {
            VarSerializer.Reset(_lastTimeRoot);
            JsonSerializer.Deserialize<ZkStub>(_lastTime, VarSerializerOptions);
        }
        else
        {
            VarSerializer.Reset(_lastNameRoot);
            JsonSerializer.Deserialize<ZkStub>($"\"{_lastName}\"", VarSerializerOptions);
        }
    }

    private void RefreshLast()
    {
        if (Config.ListingSort is ListingSort.Created)
        {
            VarSerializer.Reset(_lastTimeRoot);
            _lastTime = JsonSerializer.Deserialize<long>(JsonSerializer.SerializeToElement(ZkStub.Instance, VarSerializerOptions));
        }
        else
        {
            VarSerializer.Reset(_lastNameRoot);
            _lastName = JsonSerializer.Deserialize<string>(JsonSerializer.SerializeToElement(ZkStub.Instance, VarSerializerOptions))!;
        }
    }

    private async IAsyncEnumerable<FtpListItem> GetListing([EnumeratorCancellation]CancellationToken stoppingToken)
    {
        if (Config.FullTimeListing is null || (Config.FullTimeListing is bool b && !b))
        {
            foreach (FtpListItem item in await _client.GetListing(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                yield return item;
            }
        }
        else
        {
            string path = await _client.GetWorkingDirectory(stoppingToken);
            foreach(string line in await _client.ExecuteDownloadText("LIST --full-time", stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                Match match = FullTimeListingPattern().Match(line);
                if (match.Success)
                {
                    int chmod = 0;
                    FtpPermission ownerPermission = FtpPermission.None;
                    FtpPermission groupPermission = FtpPermission.None;
                    FtpPermission otherPermission = FtpPermission.None;

                    if (match.Groups["or"].Value == "r")
                    {
                        ownerPermission |= FtpPermission.Read;
                        chmod |= 4;
                    }
                    if (match.Groups["ow"].Value == "w")
                    {
                        ownerPermission |= FtpPermission.Write;
                        chmod |= 2;
                    }
                    if (match.Groups["ox"].Value == "x")
                    {
                        ownerPermission |= FtpPermission.Execute;
                        chmod |= 1;
                    }
                    chmod <<= 3;

                    if (match.Groups["gr"].Value == "r")
                    {
                        groupPermission |= FtpPermission.Read;
                        chmod |= 4;
                    }
                    if (match.Groups["gw"].Value == "w")
                    {
                        groupPermission |= FtpPermission.Write;
                        chmod |= 2;
                    }
                    if (match.Groups["gx"].Value == "x")
                    {
                        groupPermission |= FtpPermission.Execute;
                        chmod |= 1;
                    }
                    chmod <<= 3;

                    if (match.Groups["ar"].Value == "r")
                    {
                        otherPermission |= FtpPermission.Read;
                        chmod |= 4;
                    }
                    if (match.Groups["aw"].Value == "w")
                    {
                        otherPermission |= FtpPermission.Write;
                        chmod |= 2;
                    }
                    if (match.Groups["ax"].Value == "x")
                    {
                        otherPermission |= FtpPermission.Execute;
                        chmod |= 1;
                    }

                    if (!DateTime.TryParse(match.Groups["timestamp"].Value, out DateTime timestamp))
                    {
                        timestamp = DateTime.MinValue;
                    }

                    if (!int.TryParse(match.Groups["size"].Value, out int size))
                    {
                        size = 0;
                    }

                    FtpListItem result = new()
                    {
                        Chmod = chmod,
                        FullName = string.Format("{0}/{1}", path, match.Groups["name"].Value),
                        GroupPermissions = groupPermission,
                        OwnerPermissions = ownerPermission,
                        OthersPermissions = otherPermission,
                        Input = line,
                        Created = timestamp.ToUniversalTime(),
                        Modified = timestamp.ToUniversalTime(),
                        Name = match.Groups["name"].Value,
                        RawCreated = timestamp,
                        RawModified = timestamp,
                        RawPermissions = match.Groups["permissions"].Value,
                        RawGroup = match.Groups["gp"].Value,
                        RawOwner = match.Groups["op"].Value,
                        Type = match.Groups["d"].Value switch { 
                            "d" => FtpObjectType.Directory,
                            "l" => FtpObjectType.Link,
                            _ => FtpObjectType.File
                        },
                        SubType = match.Groups["d"].Value == "d" ? match.Groups["name"].Value switch { 
                            "." => FtpObjectSubType.SelfDirectory, 
                            ".." => FtpObjectSubType.ParentDirectory,
                            _ => FtpObjectSubType.SubDirectory
                        } : FtpObjectSubType.Unknown,
                        Size = size,
                    };
                    yield return result;
                }
            }
        }
    }
}