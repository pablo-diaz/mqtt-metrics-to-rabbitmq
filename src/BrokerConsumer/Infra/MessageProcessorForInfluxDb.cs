using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using BrokerConsumer.Services;
using BrokerConsumer.Infra.DTOs;

using CSharpFunctionalExtensions;
using InfluxDB.Client;

namespace BrokerConsumer.Infra;

public class MessageProcessorForInfluxDb: IMessageProcessor
{
    private readonly InfluxDbConfig _influxConfig;
    private readonly ProcessorConfig _processorConfig;
    private readonly InfluxDBClient _influxClient;
    private readonly WriteApiAsync _influxAsyncWritter;

    private List<string> _deviceAditionalInfoFields = new();
    private Dictionary<string, List<string>> _deviceAditionalInformation = new();

    public MessageProcessorForInfluxDb(InfluxDbConfig influxConfig, ProcessorConfig processorConfig)
    {
        this._influxConfig = influxConfig;
        this._processorConfig = processorConfig;
        this._influxClient = new InfluxDBClient(_influxConfig.ServiceUrl, _influxConfig.ServiceToken);
        this._influxAsyncWritter = this._influxClient.GetWriteApiAsync();

        var loadDeviceInfoResult = LoadDeviceInformation(fromFile: _processorConfig.DeviceInfoFilePath);
        if(loadDeviceInfoResult.IsFailure)
            throw new Exception(message: "MessageProcessorForInfluxDb couldn't be created, while reading device aditional info. Reason: " + loadDeviceInfoResult.Error);
    }

    public Task Process(string message)
    {
        var parsedMessageResult = ParseMessage(brokerMessage: message, messageFormatParts: _processorConfig.MessageParts);
        if(parsedMessageResult.IsFailure)
        {
            System.Console.WriteLine($"Broker message does not comply with expected format. Reason: {parsedMessageResult.Error}. Message: {message}");
            return Task.CompletedTask;
        }

        System.Console.WriteLine(parsedMessageResult.Value);
        return StoreMetricInInflux(parsedMessageResult.Value);
    }

    private static Result<DeviceMetric> ParseMessage(string brokerMessage, ProcessorConfig.Part[] messageFormatParts)
    {
        var expectedPartsInMessage =
              1  // Device Id
            + messageFormatParts.Length
            + 2; // one for metric Date and another for metric Time

        var messageParts = brokerMessage.Split('@');
        if(messageParts.Length != expectedPartsInMessage)
            return Result.Failure<DeviceMetric>($"{messageParts.Length} parts were found but {expectedPartsInMessage} parts were expected");

        var tracedAtResult = GetDate(fromDate: messageParts[^2], fromTime: messageParts[^1]);
        if(tracedAtResult.IsFailure)
            return Result.Failure<DeviceMetric>(tracedAtResult.Error);

        (var fields, var tags) = GetFieldsAndTags(messageParts, messageFormatParts);
        return new DeviceMetric { DeviceId = messageParts[0], Fields = fields, Tags = tags, TracedAt = tracedAtResult.Value };
    }

    private static (IEnumerable<(string Name, object Value)> Fields, IEnumerable<(string Name, string Value)> Tags) GetFieldsAndTags(string[] fromMessageParts, ProcessorConfig.Part[] withMessageFormatParts)
    {
        var fields = new List<(string Name, object Value)>();
        var tags = new List<(string Name, string Value)>();

        for(var i=1; i < fromMessageParts.Length - 2; i++)
        {
            if(withMessageFormatParts[i-1].Skip)
                continue;

            if(withMessageFormatParts[i-1].Purpose == "field")
                fields.Add((Name: withMessageFormatParts[i-1].Name, Value: Parse(value: fromMessageParts[i], withType: withMessageFormatParts[i-1].Type)));
            else if(withMessageFormatParts[i-1].Purpose == "tag")
                tags.Add((Name: withMessageFormatParts[i-1].Name, Value: fromMessageParts[i]));
        }

        return (Fields: fields, Tags: tags);
    }

    private static object Parse(string value, string withType) => withType switch {
        "number" => Decimal.Parse(value),
        "string" => value,
        _ => value
    };

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
        return _influxAsyncWritter.WritePointAsync(point: metric.MapToInfluxDataPoint(toMeasurement: _influxConfig.TargetMeasurement, withAditionalTags: GetDeviceAditionalInfo(basedOnMetric: metric)),
                                                   bucket: _influxConfig.Bucket, org: _influxConfig.Organization);
    }

    private IEnumerable<(string AditionalFieldName, string WithValue)> GetDeviceAditionalInfo(DeviceMetric basedOnMetric)
    {
        if(_deviceAditionalInformation.ContainsKey(basedOnMetric.DeviceId) == false)
            yield break;

        for(var i = 0; i < _deviceAditionalInfoFields.Count; i++)
            yield return (AditionalFieldName: _deviceAditionalInfoFields[i], WithValue: _deviceAditionalInformation[basedOnMetric.DeviceId][i]);
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

            if(line.Trim().Length == 0) continue;

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
