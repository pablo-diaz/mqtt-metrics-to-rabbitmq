using System;

using CSharpFunctionalExtensions;

namespace StopReasons.Services;

public class AvailabilityMetric
{
    public enum AvailabilityType
    {
        WORKING,
        STOPPED
    }

    public string DeviceId { get; }
    public AvailabilityType Type { get; }
    public DateTime TracedAt { get; }

    private AvailabilityMetric(string deviceId, AvailabilityType type, DateTime tracedAt)
    {
        this.DeviceId = deviceId;
        this.Type = type;
        this.TracedAt = tracedAt;
    }

    public static Result<AvailabilityMetric> From(string message, (string workingStateLabel, string stoppedStateLabel) withAllowedStates)
    {
        var messageParts = message.Split('@');
        if(messageParts.Length != 4)
            return Result.Failure<AvailabilityMetric>($"{messageParts.Length} parts were found but 4 parts were expected");

        var tracedAtResult = GetDate(fromDate: messageParts[^2], fromTime: messageParts[^1]);
        if(tracedAtResult.IsFailure)
            return Result.Failure<AvailabilityMetric>(tracedAtResult.Error);

        var parsedTypeResult = ParseAvailabilityType(from: messageParts[1], withAllowedStates: withAllowedStates);
        if(parsedTypeResult.IsFailure)
            return Result.Failure<AvailabilityMetric>(parsedTypeResult.Error);

        return new AvailabilityMetric(deviceId: messageParts[0], type: parsedTypeResult.Value, tracedAt: tracedAtResult.Value);
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

    private static Result<AvailabilityType> ParseAvailabilityType(string from, (string workingStateLabel, string stoppedStateLabel) withAllowedStates)
    {
        if(from == withAllowedStates.workingStateLabel) return AvailabilityType.WORKING;
        if(from == withAllowedStates.stoppedStateLabel) return AvailabilityType.STOPPED;
        return Result.Failure<AvailabilityType>($"UnExcepted availability type '{from}'");
    }

    public override string ToString() =>
        $"{DeviceId} - {Type} - {TracedAt:yyyy-MM-dd hh:mm:ss tt}";
}
