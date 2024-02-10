namespace Net.Leksi.MicroService.Logging;

public static class LoggerMessages
{
    public static readonly Action<ILogger, string, Exception?> LostLeadership = LoggerMessage.Define<string>(
    LogLevel.Information,
        new EventId(200001, nameof(LostLeadership)),
        "Lost leadership: {WorkerId}"
    );
    public static readonly Action<ILogger, string, Exception?> BecomeLeader = LoggerMessage.Define<string>(
    LogLevel.Information,
        new EventId(200002, nameof(BecomeLeader)),
        "Become a leader: {WorkerId}"
    );

}
