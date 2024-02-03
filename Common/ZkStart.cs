using Microsoft.Extensions.Configuration;
using org.apache.zookeeper;

namespace Net.Leksi.MicroService.Common;

public class ZkStart
{
    private class TheWather(ZkStart starter) : Watcher
    {
        public override async Task process(WatchedEvent evt)
        {
            if (!starter._mres.IsSet)
            {
                starter._mres.Set();
            }
            starter._watchedEventHandler?.Invoke(evt);
            await Task.CompletedTask;
        }
    }

    private const string s_defaultConnectionString = "localhost:2181";
    private const int s_defaultConnectionTimeout = 10000;
    private const int s_defaultSessionTimeout = 20000;

    private const string s_connectionStringParam = "zk-connection-string";
    private const string s_connectionTimeoutParam = "zk-connection-timeout";
    private const string s_sessionTimeoutParam = "zk-session-timeout";

    private ZooKeeper _zk = null!;
    private string _connectionString = null!;
    private int _connectionTimeout = 0;
    private int _sessionTimeout = 0;
    private Action<WatchedEvent>? _watchedEventHandler = null;

    private ManualResetEventSlim _mres = new();

    private ZkStart() { }
    public static ZkStart Create()
    {
        return new ZkStart();
    }

    public ZkStart WithConfiguration(IConfiguration configuration)
    {
        if (configuration[s_connectionStringParam] is string s1 && string.IsNullOrEmpty(_connectionString))
        {
            _connectionString = s1;
        }
        if (configuration[s_connectionTimeoutParam] is string s2 && _connectionTimeout == 0)
        {
            int.TryParse(s2, out _connectionTimeout);
        }
        if (configuration[s_sessionTimeoutParam] is string s3 && _sessionTimeout == 0)
        {
            int.TryParse(s3, out _sessionTimeout);
        }
        return this;
    }
    public ZkStart WithConnectionString(string connectionString)
    {
        _connectionString = connectionString;
        return this;
    }
    public ZkStart WithConnectionTimeout(int connectionTimeout)
    {
        _connectionTimeout = connectionTimeout;
        return this;
    }
    public ZkStart WithSessionTimeout(int sessionTimeout)
    {
        _sessionTimeout = sessionTimeout;
        return this;
    }
    public ZkStart WithWatchedEventHandler(Action<WatchedEvent> handler)
    {
        return this;
    }
    public ZooKeeper? Start()
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            _connectionString = s_defaultConnectionString;
        }
        if (_connectionTimeout == 0)
        {
            _connectionTimeout = s_defaultConnectionTimeout;
        }
        if (_sessionTimeout == 0)
        {
            _sessionTimeout = s_defaultSessionTimeout;
        }
        _mres.Reset();
        _zk = new ZooKeeper(_connectionString, _sessionTimeout, new TheWather(this));
        _mres.Wait(_connectionTimeout);
        if (_zk.getState() is not ZooKeeper.States.CONNECTED)
        {
            return null;
        }
        return _zk;
    }
}
