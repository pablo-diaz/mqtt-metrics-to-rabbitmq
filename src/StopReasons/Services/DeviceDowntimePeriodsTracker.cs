using System;
using System.Linq;
using System.Collections.Generic;

using CSharpFunctionalExtensions;

namespace StopReasons.Services;

public class DeviceDowntimePeriodsTracker
{
    private List<DowntimePeriod> _periods = new();

    public Result Process(AvailabilityMetric metric, Func<long> idsGeneratorFn) => metric.Type switch {
        AvailabilityMetric.AvailabilityType.WORKING => TraceWorkingMetric(at: metric.TracedAt),
        AvailabilityMetric.AvailabilityType.STOPPED => TraceStoppedMetric(at: metric.TracedAt, idsGeneratorFn),
        _ => Result.Failure($"It was not possible to process this availability metric, because its '{metric.Type}' type is not recognized")
    };

    public Result SetDowntimeReason(string reason, long forDowntimePeriodId)
    {
        var maybePeriodFound = _periods.FirstOrDefault(p => p.Id == forDowntimePeriodId) ?? Maybe<DowntimePeriod>.None;
        if(maybePeriodFound.HasNoValue)
            return Result.Failure("Cannot set downtime reason, because period cannot be found by the given id");

        maybePeriodFound.Value.SetReason(reason);
        return Result.Success();
    }

    public List<PendingDowntimePeriodToSetReasonsFor> GetPendingDowntimePeriodsToSetReasonsFor() =>
        _periods.Where(p => p.MaybeReason.HasNoValue)
                .Select(p => new PendingDowntimePeriodToSetReasonsFor(downtimePeriodId: p.Id, initiallyStoppedAt: p.InitiallyStoppedAt, lastStopReportedAt: p.LastStoppedMetricTracedAt))
                .ToList();

    public List<DowntimePeriodWithReasonSet> GetPeriodsWithReasonSet() =>
        _periods.Where(p => p.MaybeReason.HasValue)
                .Select(p => new DowntimePeriodWithReasonSet(initiallyStoppedAt: p.InitiallyStoppedAt, lastStopReportedAt: p.LastStoppedMetricTracedAt, reason: p.MaybeReason.Value))
                .ToList();

    private Result TraceStoppedMetric(DateTime at, Func<long> idsGeneratorFn)
    {
        var maybeCurrentStoppedPeriod = FindCurrentStoppedPeriod();
        if(maybeCurrentStoppedPeriod.HasValue)
        {
            maybeCurrentStoppedPeriod.Value.TraceNewStopMetric(at);
            return Result.Success();
        }

        _periods.Add(DowntimePeriod.For(stoppedAt: at, getNextIdFn: idsGeneratorFn));
        return Result.Success();
    }

    private Result TraceWorkingMetric(DateTime at)
    {
        var maybeCurrentStoppedPeriod = FindCurrentStoppedPeriod();
        if(maybeCurrentStoppedPeriod.HasNoValue)
            return Result.Success();

        maybeCurrentStoppedPeriod.Value.Finish(at);
        return Result.Success();
    }

    private Maybe<DowntimePeriod> FindCurrentStoppedPeriod() =>
        _periods.FirstOrDefault(p => p.IsItStillStopped) ?? Maybe<DowntimePeriod>.None;
}

public record PendingDowntimePeriodToSetReasonsFor(long downtimePeriodId, DateTime initiallyStoppedAt, DateTime lastStopReportedAt)
{
    public string deviceId { get; set; } = "";
}

public record DowntimePeriodWithReasonSet(DateTime initiallyStoppedAt, DateTime lastStopReportedAt, string reason)
{
    public string deviceId { get; set; } = "";
}
