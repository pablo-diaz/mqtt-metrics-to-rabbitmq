using System;
using System.Linq;
using System.Collections.Generic;

namespace BrokerConsumer.Infra.DTOs;

internal class DeviceMetric
{
    public record Field(string Name, object Value);
    public record Tag(string Name, string Value);
    
    public string DeviceId { get; init; }
    public DateTime TracedAt  { get; init; }
    public IEnumerable<Field> Fields { get; init; }
    public IEnumerable<Tag> Tags { get; init; }

    public override string ToString() =>
        $"[{TracedAt:yyyy-MM-dd} at {TracedAt:hh:mm:ss tt} - {DeviceId}] {string.Join(separator: " - ", Fields.Select(fd => $"{fd.Name}: {fd.Value}"))}";

    public InfluxDB.Client.Writes.PointData MapToInfluxDataPoint(string toMeasurement, IEnumerable<(string Name, string Value)> withAditionalTags)
    {
        var point = InfluxDB.Client.Writes.PointData.Measurement(toMeasurement);
        point = point.Tag("device_id", DeviceId);
        point = point.Timestamp(timestamp: ToUtc(TracedAt), timeUnit: InfluxDB.Client.Api.Domain.WritePrecision.Ns);

        foreach(var field in Fields)
            point = point.Field(field.Name, field.Value);

        foreach(var tag in Tags)
            point = point.Tag(tag.Name, tag.Value);

        foreach(var aditionalInfo in withAditionalTags)
            point = point.Tag(name: aditionalInfo.Name,
                              value: aditionalInfo.Value.Trim().Length > 0 ? aditionalInfo.Value.Trim() : "N/A");

        return point;
    }

    private static DateTime ToUtc(DateTime from) =>
        from.AddHours(0);  // TODO: adjust this UTC-5 "America/Bogota"
}