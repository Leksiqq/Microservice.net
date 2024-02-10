namespace Net.Leksi.MicroService.Common;

public static class Constants
{
    public const string KafkaMessageTypeName = nameof(KafkaMessageTypeName);

    public const string InvalidParamType = "Invalid parameter type: {0}. Expected {1}, got {2}.";
    public const string MissedMandatoryParam = "Missed mandatory parameter: {0}.";
    public const string ZookeeperConnectionFailed = "Zookeeper connection failed: {0}.";
    public const string MissedMandatoryProperty = "Missed mandatory property: {0}.{1}.";
    public const string LoggerProviderNotSupported = "Logger provider is not supported: {0}.";

    public const string ConfigPropertyName = "config";
    public const string NamePropertyName = "name";
    public const string KafkaPropertyName = "kafka";
    public const string StorageParamName = "storage";
    public const string LoggerPropertyName = "logger";
    public const string LoggingPropertyName = "Logging";
    public const string LogLevelPropertyName = "LogLevel";
    public const string DefaultPropertyName = "Default";
    public const string ScriptPrefix = "$";
    public const string DebugLoggerProviderName = "DebugLogMessage";
    public const string ConsoleLoggerProviderName = "Console";
    public const string KafkaLoggerProviderName = "Kafka";

}
