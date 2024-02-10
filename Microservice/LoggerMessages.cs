using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Logging;

public static class LoggerMessages
{
    public static readonly Action<ILogger, string, Exception?> LostLeadershipLogMessage = LoggerMessage.Define<string>(
    LogLevel.Information,
        new EventId(200001, nameof(LostLeadershipLogMessage)),
        "Lost leadership: {WorkerId}"
    );
    public static readonly Action<ILogger, string, Exception?> BecomeLeaderLogMessage = LoggerMessage.Define<string>(
    LogLevel.Information,
        new EventId(2000021, nameof(BecomeLeaderLogMessage)),
        "Become a leader: {WorkerId}"
    );

}
