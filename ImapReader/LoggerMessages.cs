using MailKit;

namespace Net.Leksi.MicroService.ImapReader;

public static class LoggerMessages
{
    private const string s_clientNotAuthenticated = "Imap client not authenticated {TimeSpan:o}";
    private const string s_clientNotConnected = "Imap client not connected {TimeSpan:o}!";

    public static readonly Action<ILogger, Exception?> ClientReconnecting = LoggerMessage.Define(
    LogLevel.Information,
        new EventId(300001, nameof(ClientReconnecting)),
        "Imap client [re]connecting..."
    );
    public static readonly Action<ILogger, Exception?> ClientReauthenticating = LoggerMessage.Define(
    LogLevel.Information,
        new EventId(300002, nameof(ClientReauthenticating)),
        "Imap client [re]authenticatingg..."
    );
    public static readonly Action<ILogger, Exception?> ClientReopenningFolder = LoggerMessage.Define(
    LogLevel.Information,
        new EventId(300003, nameof(ClientReopenningFolder)),
        "[re]openning folder..."
    );
    public static readonly Action<ILogger, bool, Exception?> ClienConnected = LoggerMessage.Define<bool>(
    LogLevel.Information,
        new EventId(300004, nameof(ClienConnected)),
        "Imap client connected? {Connected}"
    );
    public static readonly Action<ILogger, bool, Exception?> ClienAuthenticated = LoggerMessage.Define<bool>(
        LogLevel.Information,
        new EventId(300005, nameof(ClienAuthenticated)),
        "Imap client authenticated? {Authenticated}"
    );
    public static readonly Action<ILogger, bool, Exception?> FolderIsOpen = LoggerMessage.Define<bool>(
        LogLevel.Information,
        new EventId(300006, nameof(FolderIsOpen)),
        "Imap folder is open? {Open}"
    );
    public static readonly Action<ILogger, TimeSpan, Exception?> ClientNotConnectedWarn = LoggerMessage.Define<TimeSpan>(
    LogLevel.Warning,
        new EventId(300007, nameof(ClientNotConnectedWarn)),
        s_clientNotConnected
    );
    public static readonly Action<ILogger, TimeSpan, Exception?> ClientNotAuthenticatedWarn = LoggerMessage.Define<TimeSpan>(
        LogLevel.Warning,
        new EventId(300008, nameof(ClientNotAuthenticatedWarn)),
        s_clientNotAuthenticated
    );
    public static readonly Action<ILogger, TimeSpan, Exception?> ClientNotConnectedErr = LoggerMessage.Define<TimeSpan>(
    LogLevel.Error,
        new EventId(300009, nameof(ClientNotConnectedErr)),
        s_clientNotConnected
    );
    public static readonly Action<ILogger, TimeSpan, Exception?> ClientNotAuthenticatedErr = LoggerMessage.Define<TimeSpan>(
        LogLevel.Error,
        new EventId(300010, nameof(ClientNotAuthenticatedErr)),
        s_clientNotAuthenticated
    );
    public static readonly Action<ILogger, uint, Exception?> MessageReceived = LoggerMessage.Define<uint>(
        LogLevel.Information,
        new EventId(300011, nameof(MessageReceived)),
        "{Message}"
    );

}
