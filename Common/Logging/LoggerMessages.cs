using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common;

public static class LoggerMessages
{
    public static readonly Action<ILogger, string, string, Exception?> Debug = LoggerMessage.Define<string, string>(
    LogLevel.Debug,
        new EventId(100001, nameof(Debug)),
        "[{Location}] {Message}"
    );
    public static readonly Action<ILogger, string, string, Exception?> CriticalException = LoggerMessage.Define<string, string>(
    LogLevel.Critical,
        new EventId(100002, nameof(CriticalException)),
        "{Message} {StackTrace}"
    );
    public static readonly Action<ILogger, string, string, Exception?> WarningException = LoggerMessage.Define<string, string>(
    LogLevel.Warning,
        new EventId(100002, nameof(WarningException)),
        "{Message} {StackTrace}"
    );
    public static readonly Action<ILogger, string, string, Exception?> ErrorException = LoggerMessage.Define<string, string>(
    LogLevel.Error,
        new EventId(100002, nameof(ErrorException)),
        "{Message} {StackTrace}"
    );
}
