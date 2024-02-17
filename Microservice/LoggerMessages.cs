namespace Net.Leksi.MicroService.Logging;

public static class LoggerMessages
{
    private const string s_notOperative = "Not operative {TimeSpan:o}";
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
    public static readonly Action<ILogger, TimeSpan, Exception?> InoperativeWarning = LoggerMessage.Define<TimeSpan>(
    LogLevel.Warning,
        new EventId(200003, nameof(InoperativeWarning)),
        s_notOperative
    );
    public static readonly Action<ILogger, TimeSpan, Exception?> InoperativeError = LoggerMessage.Define<TimeSpan>(
    LogLevel.Error,
        new EventId(200004, nameof(InoperativeError)),
        s_notOperative
    );
}
