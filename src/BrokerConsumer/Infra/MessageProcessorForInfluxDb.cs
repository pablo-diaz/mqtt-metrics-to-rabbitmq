using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using BrokerConsumer.Services;
using BrokerConsumer.Infra.DTOs;

using CSharpFunctionalExtensions;
using InfluxDB.Client;

namespace BrokerConsumer.Infra;

public sealed class MessageProcessorForInfluxDb: IMessageProcessor
{
    private readonly InfluxDbConfig _influxConfig;
    private readonly ProcessorConfig _processorConfig;
    private readonly InfluxDBClient _influxClient;
    private readonly WriteApiAsync _influxAsyncWritter;

    private List<string> _deviceAdditionalInfoFields = new();
    private readonly Dictionary<string, List<string>> _deviceAdditionalInformation = new();

    private record MessageStructure(string DeviceId, DateTime TracedAt, string[] Payload);
    private delegate Result<MessageStructure> ParsingStrategy(string brokerMessage, int messageFormatPartsLength);
    
    public MessageProcessorForInfluxDb(InfluxDbConfig influxConfig, ProcessorConfig processorConfig)
    {
        this._influxConfig = influxConfig;
        this._processorConfig = processorConfig;
        this._influxClient = new InfluxDBClient(_influxConfig.ServiceUrl, _influxConfig.ServiceToken);
        this._influxAsyncWritter = this._influxClient.GetWriteApiAsync();

        var loadDeviceInfoResult = LoadDeviceInformation(fromFile: _processorConfig.DeviceInfoFilePath);
        if(loadDeviceInfoResult.IsFailure)
            throw new Exception(message: "MessageProcessorForInfluxDb couldn't be created, while reading device additional info. Reason: " + loadDeviceInfoResult.Error);
    }

    public Task Process(string message)
    {
        var parsedMessageResult = ParseMessage(brokerMessage: message, withMessageFormat: _processorConfig);
        if(parsedMessageResult.IsFailure)
        {
            Console.WriteLine($"Broker message does not comply with expected format. Reason: {parsedMessageResult.Error}. Message: {message}");
            return Task.CompletedTask;
        }

        Console.WriteLine(parsedMessageResult.Value);
        return StoreMetricInInflux(parsedMessageResult.Value);
    }

    private static Result<DeviceMetric> ParseMessage(string brokerMessage, ProcessorConfig withMessageFormat)
    {
        var parsedResult = GetParsingStrategy(fromMessageFormat: withMessageFormat)
                           .Invoke(brokerMessage, messageFormatPartsLength: withMessageFormat.MessageParts.Length);
        
        if (parsedResult.IsFailure)
            return Result.Failure<DeviceMetric>(parsedResult.Error);
        
        var (fields, tags) = GetFieldsAndTags(fromPayload: parsedResult.Value.Payload, withMessageFormatParts: withMessageFormat.MessageParts);
        return new DeviceMetric { DeviceId = parsedResult.Value.DeviceId, Fields = fields, Tags = tags, TracedAt = parsedResult.Value.TracedAt };
    }

    private static ParsingStrategy GetParsingStrategy(ProcessorConfig fromMessageFormat) =>
        fromMessageFormat.IsTimestampSent
            ? ParseMessageWithDateTimeStampExpected
            : ParseMessageWithoutDateTimeStamp;

    private static Result<MessageStructure> ParseMessageWithDateTimeStampExpected(string brokerMessage, int messageFormatPartsLength)
    {
        var expectedPartsInMessage =
              1  // Device Id
            + messageFormatPartsLength
            + 2; // one for metric Date and another for metric Time
        
        var messageParts = brokerMessage.Split('@');
        if(messageParts.Length != expectedPartsInMessage)
            return Result.Failure<MessageStructure>($"{messageParts.Length} parts were found but {expectedPartsInMessage} parts were expected");

        var tracedAtResult = GetDate(fromDate: messageParts[^2], fromTime: messageParts[^1]);
        return tracedAtResult.IsFailure 
            ? Result.Failure<MessageStructure>(tracedAtResult.Error)
            : new MessageStructure(DeviceId: messageParts[0], TracedAt: tracedAtResult.Value, Payload: messageParts[1..^2]);
    }
    
    private static Result<MessageStructure> ParseMessageWithoutDateTimeStamp(string brokerMessage, int messageFormatPartsLength)
    {
        var expectedPartsInMessage =
            1 // Device Id
            + messageFormatPartsLength;
        
        var messageParts = brokerMessage.Split('@');
        return messageParts.Length != expectedPartsInMessage 
            ? Result.Failure<MessageStructure>($"{messageParts.Length} parts were found but {expectedPartsInMessage} parts were expected") 
            : new MessageStructure(DeviceId: messageParts[0], TracedAt: DateTime.Now, Payload: messageParts[1..]);
    }

    private static (IEnumerable<DeviceMetric.Field> Fields, IEnumerable<DeviceMetric.Tag> Tags) GetFieldsAndTags(string[] fromPayload, ProcessorConfig.Part[] withMessageFormatParts)
    {
        var fields = new List<DeviceMetric.Field>();
        var tags = new List<DeviceMetric.Tag>();

        for(var i=0; i < fromPayload.Length; i++)
        {
            var dataDescription = withMessageFormatParts[i];
            if(dataDescription.Skip)
                continue;

            if(dataDescription.Purpose == "field")
                fields.Add(new DeviceMetric.Field(Name: dataDescription.Name, Value: Parse(value: fromPayload[i], withType: dataDescription.Type)));
            else if(dataDescription.Purpose == "tag")
                tags.Add(new DeviceMetric.Tag(Name: dataDescription.Name, Value: fromPayload[i]));
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

        return new DateTime(kind: DateTimeKind.Local,
            date: new DateOnly(year: int.Parse(dateParts[0]), month: int.Parse(dateParts[1]), day: int.Parse(dateParts[2])),
            time: new TimeOnly(hour: int.Parse(timeParts[0]), minute: int.Parse(timeParts[1]), second: int.Parse(timeParts[2])));
    }

    private Task StoreMetricInInflux(DeviceMetric metric)
    {
        return _influxAsyncWritter.WritePointAsync(point: metric.MapToInfluxDataPoint(toMeasurement: _influxConfig.TargetMeasurement, withAditionalTags: GetDeviceAdditionalInfo(basedOnMetric: metric)),
                                                   bucket: _influxConfig.Bucket, org: _influxConfig.Organization);
    }

    private IEnumerable<(string AditionalFieldName, string WithValue)> GetDeviceAdditionalInfo(DeviceMetric basedOnMetric)
    {
        if(_deviceAdditionalInformation.TryGetValue(basedOnMetric.DeviceId, out var additionalFieldsForDevice) == false)
            yield break;

        for(var i = 0; i < _deviceAdditionalInfoFields.Count; i++)
            yield return (AditionalFieldName: _deviceAdditionalInfoFields[i], WithValue: additionalFieldsForDevice[i]);
    }

    private Result LoadDeviceInformation(string fromFile)
    {
        var lineCount = 0;
        foreach(var line in System.IO.File.ReadAllLines(fromFile))
        {
            lineCount++;
            if(lineCount == 1) // is CSV Header
            {
                _deviceAdditionalInfoFields = ParseCsvHeaderLine(line);
                continue;
            }

            if(line.Trim().Length == 0) continue;

            var (deviceId, deviceValues) = ParseCsvLine(line);
            _deviceAdditionalInformation[deviceId] = deviceValues;
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
