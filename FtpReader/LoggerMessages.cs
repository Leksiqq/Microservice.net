namespace Net.Leksi.MicroService.FtpReader;

public static class LoggerMessages
{
    private const string s_clientNotAuthenticated = "Ftp client not authenticated {TimeSpan:o}";
    private const string s_clientNotConnected = "Ftp client not connected {TimeSpan:o}!";

    public static readonly Action<ILogger, Exception?> ClientReconnecting = LoggerMessage.Define(
    LogLevel.Information,
        new EventId(400001, nameof(ClientReconnecting)),
        "Ftp client [re]connecting..."
    );
    public static readonly Action<ILogger, bool, Exception?> ClientConnected = LoggerMessage.Define<bool>(
    LogLevel.Information,
        new EventId(400002, nameof(ClientConnected)),
        "Ftp client connected? {Connected}"
    );
    public static readonly Action<ILogger, bool, Exception?> ClientAuthenticated = LoggerMessage.Define<bool>(
        LogLevel.Information,
        new EventId(400003, nameof(ClientAuthenticated)),
        "Ftp client authenticated? {Authenticated}"
    );
    public static readonly Action<ILogger, TimeSpan, Exception?> ClientNotConnectedWarn = LoggerMessage.Define<TimeSpan>(
    LogLevel.Warning,
        new EventId(400004, nameof(ClientNotConnectedWarn)),
        s_clientNotConnected
    );
    public static readonly Action<ILogger, TimeSpan, Exception?> ClientNotAuthenticatedWarn = LoggerMessage.Define<TimeSpan>(
        LogLevel.Warning,
        new EventId(400005, nameof(ClientNotAuthenticatedWarn)),
        s_clientNotAuthenticated
    );
    public static readonly Action<ILogger, TimeSpan, Exception?> ClientNotConnectedErr = LoggerMessage.Define<TimeSpan>(
    LogLevel.Error,
        new EventId(400006, nameof(ClientNotConnectedErr)),
        s_clientNotConnected
    );
    public static readonly Action<ILogger, TimeSpan, Exception?> ClientNotAuthenticatedErr = LoggerMessage.Define<TimeSpan>(
        LogLevel.Error,
        new EventId(400007, nameof(ClientNotAuthenticatedErr)),
        s_clientNotAuthenticated
    );
    public static readonly Action<ILogger, Exception?> ClientReopenningFolder = LoggerMessage.Define(
    LogLevel.Information,
        new EventId(400008, nameof(ClientReopenningFolder)),
        "[re]openning folder..."
    );
    public static readonly Action<ILogger, bool, Exception?> FolderIsOpen = LoggerMessage.Define<bool>(
        LogLevel.Information,
        new EventId(400009, nameof(FolderIsOpen)),
        "Ftp folder is open? {Open}"
    );
}
