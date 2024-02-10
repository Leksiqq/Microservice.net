using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common;

public static class LoggerMessages
{
    public static readonly Action<ILogger, string, string, Exception?> Debug = LoggerMessage.Define<string, string>(
    LogLevel.Debug,
        new EventId(100001, nameof(Debug)),
        "[{Location}] {Message}"
    );
    public static readonly Action<ILogger, string, string, Exception?> Exception = LoggerMessage.Define<string, string>(
    LogLevel.Critical,
        new EventId(100002, nameof(Exception)),
        "{Message} {StackTrace}"
    );
}
