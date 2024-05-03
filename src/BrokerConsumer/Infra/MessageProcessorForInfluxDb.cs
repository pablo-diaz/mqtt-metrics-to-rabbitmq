using System;
using System.Threading.Tasks;

using BrokerConsumer.Services;

using CSharpFunctionalExtensions;

namespace BrokerConsumer.Infra;

public class MessageProcessorForInfluxDb: IMessageProcessor
{
    private class DeviceMetric
    {
        public string DeviceId { get; init; }
        public decimal Temperature { get; init; }
        public DateTime TracedAt  { get; init; }

        public override string ToString() =>
            $"[{TracedAt:yyyy-MM-dd} at {TracedAt:hh:mm:ss tt} - {DeviceId}]: Temp {Temperature}";
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
        return Task.CompletedTask;
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

    public void Dispose()
    {
        // TODO: complete here
    }
}
