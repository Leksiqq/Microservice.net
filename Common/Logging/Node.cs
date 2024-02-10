using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common;

public class Node
{
    private LogLevel _minLevel = LogLevel.None;
    private bool _isMinLevelSet = false;
    public Dictionary<string, Node> Children { get; private init; } = [];
    public LogLevel MinLevel
    {
        get => _minLevel;
        internal set
        {
            _minLevel = value;
            _isMinLevelSet = true;
        }
    }
    internal bool IsMinLevelSet => _isMinLevelSet;
}
