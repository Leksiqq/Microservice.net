using MailKit;
using MailKit.Search;
using MimeKit;
using MimeKit.Encodings;
using Net.Leksi.MicroService.Common;
using Net.Leksi.ZkJson;
using Org.BouncyCastle.Utilities;
using System.Net.Mail;
using System.Text.Json;

namespace Net.Leksi.MicroService.ImapReader;

public class Worker : TemplateWorker<Config>
{
    private const string s_lastUidPath = "lastuid";
    private const string s_mimeTypeFormat = "{0}/{1}";
    private readonly Client _client = null!;
    private readonly JsonSerializerOptions _mimeJsonSerializerOption = new() { WriteIndented = true, };
    private readonly ICloudClient _storage = null!;
    private readonly KafkaProducer _kafkaProducer = null!;
    
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
        _storage = _services.GetRequiredKeyedService<ICloudClient>("storage");
        _kafkaProducer = _services.GetRequiredKeyedService<KafkaProducer>("kafka");
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
            else if (_folder is null || !_folder.IsOpen)
            {
                LoggerMessages.ClientReopenningFolder(_logger, null);
            }
        }
        try
        {
            if (!_client.IsConnected)
            {
                await _client.ConnectAsync(Config.Host ?? "localhost", Config.Port ?? 0, false, stoppingToken);
            }
            if (!_client.IsAuthenticated)
            {
                await _client.AuthenticateAsync(Config.Login, Config.Password, stoppingToken);
            }
            if (_folder is null || !_folder.IsOpen)
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

        if (uids.Count > 0)
        {
            foreach (UniqueId item in await _folder.SearchAsync(SearchQuery.Uids(uids), stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested || !CanOperate())
                {
                    break;
                }
                IList<IMessageSummary> summary = _folder.Fetch(new UniqueId[] { item }, MessageSummaryItems.Flags);
                bool deleted = summary.FirstOrDefault()?.Flags!.Value.HasFlag(MessageFlags.Deleted) ?? false;
                if (!deleted)
                {
                    _lastUid = item.Id;

                    if (_logger?.IsEnabled(LogLevel.Information) ?? false)
                    {
                        LoggerMessages.MessageReceived(_logger, item.Id, null);
                    }


                    MimeMessage message = await _folder.GetMessageAsync(item, stoppingToken);

                    StoredFolder storedFolder = _services.GetRequiredService<StoredFolder>();

                    foreach (var h in message.Headers)
                    {
                        storedFolder.Headers.Add(new KeyValuePair<string, string>(h.Field, h.Value));
                    }

                    MimeIterator iter = new(message);

                    MemoryStream ms = new();

                    while (iter.MoveNext())
                    {
                        if (iter.Current is MimePart part)
                        {
                            if (part.IsAttachment)
                            {
                                StoredFile storedFile = new();
                                storedFolder.Files.Add(storedFile);

                                foreach (var h in part.Headers)
                                {
                                    storedFile.Headers.Add(new KeyValuePair<string, string>(h.Field, h.Value));
                                }

                                ms.SetLength(0);
                                part.Content.WriteTo(ms);
                                ms.Flush();
                                ms.Position = 0;

                                if(part.ContentTransferEncoding is ContentEncoding.Base64)
                                {
                                    byte[] bytes = Convert.FromBase64String(new StreamReader(ms).ReadToEnd());
                                    ms.SetLength(0);
                                    ms.Write(bytes);
                                    ms.Flush();
                                    ms.Position = 0;
                                }
                                else if(part.ContentTransferEncoding is ContentEncoding.QuotedPrintable)
                                {
                                    Attachment hack = Attachment.CreateAttachmentFromString(string.Empty, new StreamReader(ms).ReadToEnd());
                                    ms.SetLength(0);
                                    new StreamWriter(ms).Write(hack.Name);
                                    ms.Flush();
                                    ms.Position = 0;
                                }


                                storedFile.Path = await _storage.UploadFile(
                                    ms, 
                                    part.ContentType.Name, 
                                    string.Format(s_mimeTypeFormat, part.ContentType.MediaType, part.ContentType.MediaSubtype), 
                                    ms.Length, 
                                    stoppingToken
                                );
                            }

                        }
                    }

                    await _kafkaProducer.ProduceAsync(
                        new ReceivedFilesKafkaMessage { StoredFolder = storedFolder, },
                        stoppingToken
                    );

                    if (Config.DeleteAfterDownload is bool b && b)
                    {
                        await _folder.AddFlagsAsync(item, MessageFlags.Deleted, true, stoppingToken);
                    }
                }
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
        for (uint id = start; id < finish; ++id)
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
        JsonSerializer.Deserialize<ZkStub>(_lastUid, VarSerializerOptions);
    }
}
