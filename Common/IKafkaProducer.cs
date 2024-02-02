namespace Net.Leksi.MicroService.Common;

public interface IKafkaProducer
{
    string Kind { get; }
    string Name { get; }
}
