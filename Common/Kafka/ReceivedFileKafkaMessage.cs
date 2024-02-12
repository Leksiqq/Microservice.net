namespace Net.Leksi.MicroService.Common;

public class ReceivedFileKafkaMessage : KafkaMessageBase
{
    public string Path { get; set; } = null!;
}
