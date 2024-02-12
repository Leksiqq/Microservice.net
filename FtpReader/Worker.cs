using FluentFTP;
using Net.Leksi.MicroService.Common;
using System.Net;

namespace Net.Leksi.MicroService.FtpReader;
public class Worker : TemplateWorker<Config>
{
    private class FtpLogger(ILogger<TemplateWorker<Config>> logger) : IFtpLogger
    {
        public void Log(FtpLogEntry entry)
        {
            LogLevel ll = entry.Severity switch 
            {
                FtpTraceLevel.Info => LogLevel.Information,
                FtpTraceLevel.Warn => LogLevel.Warning,
                FtpTraceLevel.Error => LogLevel.Error,
                _ => LogLevel.None
            };
            logger.LogInformation(entry.Message);
        }
    }
    private readonly ICloudClient _storage = null!;
    private readonly IKafkaProducer _kafkaProducer = null!;
    private readonly string _pathPrefix;
    private readonly AsyncFtpClient _client;
    private bool _folderIsOpen = false;
    protected override bool IsOperative => _client.IsConnected && _client.IsAuthenticated && _folderIsOpen;
    public Worker(IServiceProvider services) : base(services)
    {
        IsSingleton = true;
        DefaultConfig.Folder = "/";
        DefaultConfig.Port = 21;
        _client = new AsyncFtpClient();
        _client.Logger = new FtpLogger(_logger);
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
        Console.WriteLine(_client.Host);
        Console.WriteLine(_client.Port);
        Console.WriteLine(_client.Credentials);
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
            else if (!_folderIsOpen)
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
        await Task.CompletedTask;
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