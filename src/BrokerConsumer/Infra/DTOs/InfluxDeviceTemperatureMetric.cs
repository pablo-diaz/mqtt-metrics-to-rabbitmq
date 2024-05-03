using System;

using InfluxDB.Client.Core;

namespace BrokerConsumer.Infra.DTOs;

[Measurement("device-temperature-metric")]
internal class InfluxDeviceTemperatureMetric
{
    [Column("device-id", IsTag = true)]
    public string DeviceId { get; init; }

    [Column("temperature")]
    public decimal Temperature { get; init; }

    [Column(IsTimestamp = true)]
    public DateTimeOffset LoggedAt { get; init; }
}
