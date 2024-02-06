namespace Net.Leksi.MicroService.Common;

public class ReceivedFileKafkaMessage : KafkaMessage
{
    public string Path { get; set; } = null!;
}
