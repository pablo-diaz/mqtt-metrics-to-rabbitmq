using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using BrokerConsumer.Services;
using BrokerConsumer.Infra.DTOs;

using CSharpFunctionalExtensions;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using Newtonsoft.Json.Linq;

namespace BrokerConsumer.Infra;

public class MessageProcessorForInfluxDb: IMessageProcessor
{
    private readonly InfluxDbConfig _influxConfig;
    private readonly InfluxDBClient _influxClient;
    private readonly WriteApiAsync _influxAsyncWritter;

    private List<string> _deviceAditionalInfoFields = new();
    private Dictionary<string, List<string>> _deviceAditionalInformation = new();

    private class DeviceMetric
    {
        public string DeviceId { get; init; }
        public decimal Temperature { get; init; }
        public DateTime TracedAt  { get; init; }

        public override string ToString() =>
            $"[{TracedAt:yyyy-MM-dd} at {TracedAt:hh:mm:ss tt} - {DeviceId}]: Temp {Temperature}";

        public DTOs.InfluxDeviceTemperatureMetric MapToInfluxMeasurement() =>
            new DTOs.InfluxDeviceTemperatureMetric {
                DeviceId = DeviceId,
                Temperature = Temperature,
                LoggedAt = TracedAt
            };

        public InfluxDB.Client.Writes.PointData MapToInfluxDataPoint(IEnumerable<(string AditionalFieldName, string WithValue)> withDeviceAditionalInfo)
        {
            var point = InfluxDB.Client.Writes.PointData.Measurement("device-temperature-metric");
            point = point.Tag("device-id", DeviceId);
            point = point.Field("temperature", Temperature);
            point = point.Timestamp(timestamp: TracedAt.ToUniversalTime(), timeUnit: WritePrecision.Ns);

            foreach(var aditionalInfo in withDeviceAditionalInfo)
                point = point.Tag(name: aditionalInfo.AditionalFieldName,
                                  value: aditionalInfo.WithValue.Trim().Length > 0 ? aditionalInfo.WithValue.Trim() : "N/A");

            return point;
        }
    }
    
    public MessageProcessorForInfluxDb(InfluxDbConfig influxConfig, ProcessorConfig processorConfig)
    {
        this._influxConfig = influxConfig;
        this._influxClient = new InfluxDBClient(_influxConfig.ServiceUrl, _influxConfig.ServiceToken);
        this._influxAsyncWritter = this._influxClient.GetWriteApiAsync();

        var loadDeviceInfoResult = LoadDeviceInformation(fromFile: processorConfig.DeviceInfoFilePath);
        if(loadDeviceInfoResult.IsFailure)
            throw new Exception(message: "MessageProcessorForInfluxDb couldn't be created, while reading device info. Reason: " + loadDeviceInfoResult.Error);
    }

    public Task Process(string message)
    {
        var parsedMessageResult = ParseMessage(message);
        if(parsedMessageResult.IsFailure)
        {
            System.Console.WriteLine($"Broker message does not comply with expected format. Reason: {parsedMessageResult.Error}. Message: {message}");
            return Task.CompletedTask;
        }

        System.Console.WriteLine(parsedMessageResult.Value);
        return StoreMetricInInflux(parsedMessageResult.Value);
    }

    private static Result<DeviceMetric> ParseMessage(string brokerMessage)
    {
        var messageParts = brokerMessage.Split('@');
        if(messageParts.Length != 4)
            return Result.Failure<DeviceMetric>($"{messageParts.Length} parts were found and 4 parts were expected");

        var tracedAtResult = GetDate(fromDate: messageParts[2], fromTime: messageParts[3]);
        if(tracedAtResult.IsFailure)
            return Result.Failure<DeviceMetric>(tracedAtResult.Error);

        return new DeviceMetric {
            DeviceId = messageParts[0],
            Temperature = decimal.Parse(messageParts[1]),  // TODO: parse decimal, considering that string may have coma and not dot, for decimal separator
            TracedAt = tracedAtResult.Value
        };
    }

    private static Result<DateTime> GetDate(string fromDate, string fromTime)
    {
        var dateParts = fromDate.Split('-');
        if(dateParts.Length != 3)
            return Result.Failure<DateTime>($"Date part has {dateParts.Length} parts but 3 parts were expected");

        var timeParts = fromTime.Split('_');
        if(timeParts.Length != 3)
            return Result.Failure<DateTime>($"Time part has {timeParts.Length} parts but 3 parts were expected");

        return new DateTime(int.Parse(dateParts[0]), int.Parse(dateParts[1]), int.Parse(dateParts[2]), int.Parse(timeParts[0]), int.Parse(timeParts[1]), int.Parse(timeParts[2]));
    }

    private Task StoreMetricInInflux(DeviceMetric metric)
    {
        return _influxAsyncWritter.WritePointAsync(point: metric.MapToInfluxDataPoint(withDeviceAditionalInfo: GetDeviceAditionalInfo(basedOnMetric: metric)),
                                                   bucket: _influxConfig.Bucket, org: _influxConfig.Organization);
    }

    private IEnumerable<(string AditionalFieldName, string WithValue)> GetDeviceAditionalInfo(DeviceMetric basedOnMetric)
    {
        var deviceIdToSearch = basedOnMetric.DeviceId.Split(separator: '_')[1];
        if(_deviceAditionalInformation.ContainsKey(deviceIdToSearch) == false)
            yield break;

        for(var i = 0; i < _deviceAditionalInfoFields.Count; i++)
            yield return (AditionalFieldName: _deviceAditionalInfoFields[i], WithValue: _deviceAditionalInformation[deviceIdToSearch][i]);
    }

    private Result LoadDeviceInformation(string fromFile)
    {
        var lineCount = 0;
        foreach(var line in System.IO.File.ReadAllLines(fromFile))
        {
            lineCount++;
            if(lineCount == 1) // is CSV Header
            {
                _deviceAditionalInfoFields = ParseCsvHeaderLine(line);
                continue;
            }

            (var deviceId, var deviceValues) = ParseCsvLine(line);
            _deviceAditionalInformation[deviceId] = deviceValues;
        }

        return Result.Success();
    }

    private static List<string> ParseCsvHeaderLine(string line) =>
        line.Split(separator: ',')
            .Skip(1)  // do not consider the DeviceId column name
            .ToList();

    private static (string DeviceId, List<string> Values) ParseCsvLine(string forDeviceInfo)
    {
        var lineContent = forDeviceInfo.Split(separator: ',');
        return (DeviceId: lineContent.First(),
                Values: lineContent
                        .Skip(1)  // do not consider DeviceId
                        .ToList()
            );
    }

    public void Dispose()
    {
        _influxClient?.Dispose();
    }
}
