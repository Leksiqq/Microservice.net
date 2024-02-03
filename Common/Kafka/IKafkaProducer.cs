﻿using Confluent.Kafka;

namespace Net.Leksi.MicroService.Common;

public interface IKafkaProducer: IDisposable
{
    Task<List<DeliveryResult<string, string>>> ProduceAsync(Message<string, string> message, CancellationToken stoppingToken);
}