using Microsoft.Extensions.Logging;

namespace Net.Leksi.MicroService.Common;

public class KafkaLoggerProvider : ILoggerProvider
{

    public ILogger CreateLogger(string categoryName)
    {
        Console.WriteLine(categoryName);
        return new KafkaLogger();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
