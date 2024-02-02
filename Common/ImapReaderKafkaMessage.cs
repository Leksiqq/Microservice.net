namespace Net.Leksi.MicroService.Common;

public class ImapReaderKafkaMessage: KafkaMessage
{
    public string Path { get; set; } = null!;
}
