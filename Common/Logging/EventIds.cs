using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common;

public static class EventIds
{
    private static readonly Dictionary<string, EventId> _ids = [];
    static EventIds()
    {
        Add("DebugLogMessage",                    10001);
        Add("ExceptionThrownLogMessage",          10002);
        Add("s_becomeALeaderLogMessage",          10003);
        Add("s_lostLeadershipLogMessage",         10004);
        Add($"{nameof(Worker)}.{nameof(Worker.s_clientReconnectingLogMessage)}",     10005);
        Add("s_clientReauthenticatingLogMessage", 10006);
        Add("s_clientReopenningFolderLogMessage", 10007);
        Add("s_clienConnectedLogMessage",         10008);
    }
    public static EventId Get(string key)
    {
        if(_ids.TryGetValue(key, out EventId eid) && eid.Id != 0)
        {
            return eid;
        }
        throw new ArgumentException(nameof(key));
    }
    public static void Add(string key, int id)
    {
        if (_ids.Values.Any(eid => eid.Id == id))
        {
            throw new ArgumentException(nameof(id));
        }
        _ids.Add(key, new EventId(id, key));
    }
}
