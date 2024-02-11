using Microsoft.Extensions.Logging;
using System.Data;
using System.Text.Json;

namespace Net.Leksi.MicroService.Common;

public class LoggingManager
{
    private const string s_in = " in ";
    private const string s_loggerProvider = "{0}LoggerProvider";
    private ILogger _debugLogger = null!;
    private readonly Dictionary<string, Node> _providers = [];
    private bool _canDebug = false;
    public void Debug(string message)
    {
        if (_canDebug && _debugLogger is { })
        {
            LoggerMessages.Debug(_debugLogger, GetLocation(), message, null);
        }
    }
    public void SetLoggerFactory(ILoggerFactory loggerFactory)
    {
        _debugLogger ??= loggerFactory.CreateLogger("Debug");
    }
    public void Configure(ILoggingBuilder builder, JsonElement config, JsonSerializerOptions jsonSerializerOptions)
    {
        builder.ClearProviders();
        foreach (JsonProperty prop in config.EnumerateObject().Where(e => !e.Name.Equals(Constants.LogLevelPropertyName, StringComparison.OrdinalIgnoreCase)))
        {
            if (prop.Name.Equals(Constants.DebugLoggerProviderName, StringComparison.OrdinalIgnoreCase))
            {
                builder.AddDebug();
            }
            else if (prop.Name.Equals(Constants.ConsoleLoggerProviderName, StringComparison.OrdinalIgnoreCase))
            {
                builder.AddConsole();
            }
            else if (prop.Name.Equals(Constants.KafkaLoggerProviderName, StringComparison.OrdinalIgnoreCase))
            {
                if (
                    prop.Value.EnumerateObject().Where(
                        e => e.Name.Equals(Constants.LoggerPropertyName, StringComparison.OrdinalIgnoreCase)
                    ).Select(e => e.Value).FirstOrDefault() is JsonElement json
                    && json.ValueKind is not JsonValueKind.Undefined
                )
                {
                    KafkaLoggerConfig conf = JsonSerializer.Deserialize<KafkaLoggerConfig>(json, jsonSerializerOptions)!;
                    builder.AddProvider(new KafkaLoggerProvider(conf, Filter));
                }

            }
            else
            {
                throw new InvalidOperationException(string.Format(Constants.LoggerProviderNotSupported, prop.Name));
            }
            Node node = new();
            _providers.Add(string.Format(s_loggerProvider, prop.Name), node);
            if (
                prop.Value.EnumerateObject().Where(e => e.Name.Equals(Constants.LogLevelPropertyName, StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.Value).FirstOrDefault() is JsonElement logLevel
                && logLevel.ValueKind is not JsonValueKind.Undefined
            )
            {
                Fill(node, logLevel);
            }
        }
        if (
            config.EnumerateObject().Where(e => e.Name.Equals(Constants.LogLevelPropertyName, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Value).FirstOrDefault() is JsonElement logLevel0
            && logLevel0.ValueKind is not JsonValueKind.Undefined
        )
        {
            foreach (Node node in _providers.Values)
            {
                Fill(node, logLevel0);
            }
        }
        builder.AddFilter(Filter);

    }
    private static string GetLocation()
    {
        //return Environment.StackTrace;
        string line = Environment.StackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[3];
        return line[(line.LastIndexOf(s_in) + 4)..];
    }
    private void Fill(Node node, JsonElement json)
    {
        if (
            json.EnumerateObject().Where(e => e.Name.Equals(Constants.DefaultPropertyName, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Value).FirstOrDefault() is JsonElement defaultLevel
            && defaultLevel.ValueKind is not JsonValueKind.Undefined
        )
        {
            if (Enum.TryParse(defaultLevel.GetString(), out LogLevel ll))
            {
                if (ll <= LogLevel.Debug)
                {
                    _canDebug = true;
                }
                node.MinLevel = ll;
            }
        }
        foreach (JsonProperty prop in json.EnumerateObject().Where(e => !e.Name.Equals(Constants.DefaultPropertyName, StringComparison.OrdinalIgnoreCase)))
        {
            string[] parts = prop.Name.Split('.');
            Node cur = node;
            for (int i = 0; i < parts.Length; ++i)
            {
                if (!cur.Children.TryGetValue(parts[i], out Node? next))
                {
                    next = new Node();
                    cur.Children.Add(parts[i], next);
                    if (i < parts.Length - 1)
                    {
                        next.MinLevel = cur.MinLevel;
                    }
                    cur = next;
                }
            }
            if (!cur.IsMinLevelSet && Enum.TryParse(prop.Value.GetString(), out LogLevel ll))
            {
                if (ll <= LogLevel.Debug)
                {
                    _canDebug = true;
                }
                cur.MinLevel = ll;
            }
        }
    }
    private bool Filter(string? providerName, string? category, LogLevel logLevel)
    {
        bool result = false;
        if (providerName?.LastIndexOf('.') is int pos)
        {
            if ((pos < 0 ? providerName : providerName[(pos + 1)..]) is string key && _providers.TryGetValue(key, out Node? node))
            {
                string[] parts = category?.Split('.') ?? [];
                Node cur = node;
                for (int i = 0; i < parts.Length; ++i)
                {
                    if (!cur.Children.TryGetValue(parts[i], out Node? next))
                    {
                        break;
                    }
                    cur = next;
                }
                result = logLevel >= cur.MinLevel;
            }
        }
        return result;
    }
}
