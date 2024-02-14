using System.Net.Mail;

namespace Net.Leksi.MicroService.Common;

public class ReceivedFilesKafkaMessage : KafkaMessageBase
{
    public StoredFolder StoredFolder { get; set; } = null!;
}
