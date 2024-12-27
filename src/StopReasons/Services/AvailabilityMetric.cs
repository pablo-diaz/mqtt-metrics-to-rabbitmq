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
    public Maybe<string> MaybeDowntimeReason { get; }

    private static readonly string _StopReasonHasNotBeenSetYet = "-";

    private AvailabilityMetric(string deviceId, AvailabilityType type, DateTime tracedAt, Maybe<string> maybeDowntimeReason)
    {
        this.DeviceId = deviceId;
        this.Type = type;
        this.TracedAt = tracedAt;
        this.MaybeDowntimeReason = maybeDowntimeReason;
    }

    public static Result<AvailabilityMetric> From(string message, (string workingStateLabel, string stoppedStateLabel) withAllowedStates)
    {
        var messageParts = message.Split('@');
        if(messageParts.Length < 3)
            return Result.Failure<AvailabilityMetric>($"At least 3 parts were expected (DeviceId, State and Maybe stopping reason) but {messageParts.Length} parts were found");

        var parsedTypeResult = ParseAvailabilityType(from: messageParts[1], withAllowedStates: withAllowedStates);
        if(parsedTypeResult.IsFailure)
            return Result.Failure<AvailabilityMetric>(parsedTypeResult.Error);

        var maybeDowntimeReasonResult = GetDowntimeReason(from: messageParts[2], givenAvailability: parsedTypeResult.Value);
        if(maybeDowntimeReasonResult.IsFailure)
            return Result.Failure<AvailabilityMetric>(maybeDowntimeReasonResult.Error);

        return new AvailabilityMetric(deviceId: messageParts[0], type: parsedTypeResult.Value, tracedAt: DateTime.Now, maybeDowntimeReason: maybeDowntimeReasonResult.Value);
    }

    private static Result<AvailabilityType> ParseAvailabilityType(string from, (string workingStateLabel, string stoppedStateLabel) withAllowedStates)
    {
        if(from == withAllowedStates.workingStateLabel) return AvailabilityType.WORKING;
        if(from == withAllowedStates.stoppedStateLabel) return AvailabilityType.STOPPED;
        return Result.Failure<AvailabilityType>($"UnExcepted availability type '{from}'");
    }

    private static Result<Maybe<string>> GetDowntimeReason(string from, AvailabilityType givenAvailability) => givenAvailability switch {
        AvailabilityType.WORKING => Maybe<string>.None,
        AvailabilityType.STOPPED => from == _StopReasonHasNotBeenSetYet ? Maybe<string>.None : Maybe<string>.From(from),
        _ => Result.Failure<Maybe<string>>($"UnExcepted availability type '{givenAvailability}'")
    };

    public override string ToString() =>
        $"{DeviceId} - {Type} - {TracedAt:yyyy-MM-dd hh:mm:ss tt}";
}
