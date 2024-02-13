using FluentFTP;
using Net.Leksi.MicroService.Common;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Net.Leksi.MicroService.FtpReader;
public class Worker : TemplateWorker<Config>
{
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
    private readonly IKafkaProducer _kafkaProducer = null!;
    private readonly string _pathPrefix;
    private readonly AsyncFtpClient _client;
    private bool _folderIsOpen = false;
    private Regex? _pattern = null; 
    protected override bool IsOperative => _client.IsConnected && _client.IsAuthenticated && _folderIsOpen;
    public Worker(IServiceProvider services) : base(services)
    {
        IsSingleton = true;
        DefaultConfig.Login = "anonymous";
        DefaultConfig.Port = 21;
        _client = new AsyncFtpClient();
        _storage = _services.GetRequiredService<ICloudClient>();
        _kafkaProducer = _services.GetRequiredService<IKafkaProducer>();
        _pathPrefix = Util.CollapseSlashes($"{_storage.Bucket}:{_storage.Folder}/");
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
        _client.Host = Config.Host;
        _client.Port = Config.Port;
        _client.Credentials = new NetworkCredential(Config.Login, Config.Password);
        if (Config.LogClient)
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
        _client.Config.TimeConversion = FtpDate.UTC;
        _client.Config.ListingCustomParser = (l, f, c) =>
        {
            Console.WriteLine(l);
            return null;
        };

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
        Console.WriteLine(_client.Encoding);
        Console.WriteLine(await _client.GetWorkingDirectory(stoppingToken));
        FtpListItem[] list = await _client.GetListing(stoppingToken);
        foreach (FtpListItem item in list)
        {
            if(item.Type is FtpObjectType.File && (_pattern?.IsMatch(item.Name) ?? true))
            {
                Console.WriteLine($"{item.FullName}, {item.RawCreated}, {item.Created}");
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
}