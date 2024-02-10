using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common.Logging;

public static class LoggerMessages
{
    public static readonly Action<ILogger, string, string, string, Exception?> DebugLogMessage = LoggerMessage.Define<string, string, string>(
    LogLevel.Debug,
        new EventId(100001, nameof(DebugLogMessage)),
        "[{Location}] {Label}: {Value}"
    );
    public static readonly Action<ILogger, string, string, Exception?> ExceptionLogMessage = LoggerMessage.Define<string, string>(
    LogLevel.Critical,
        new EventId(100002, nameof(ExceptionLogMessage)),
        "{Message} {StackTrace}"
    );
}
