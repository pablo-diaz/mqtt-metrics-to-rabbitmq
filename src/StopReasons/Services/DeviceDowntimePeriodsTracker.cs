using System;
using System.Linq;
using System.Collections.Generic;

using CSharpFunctionalExtensions;

namespace StopReasons.Services;

public class DeviceDowntimePeriodsTracker
{
    private List<DowntimePeriod> _periods = new();

    public void AddPeriod(IAvailabilityMetricStorage.AvailabilityMetricInStorage fromPersistenceMetric)
    {
        _periods.Add(DowntimePeriod.For(fromPersistenceMetric));
    }

    public Result Process(AvailabilityMetric metric, Func<long> idsGeneratorFn, (Action<long> OnAdding, Action<long> OnUpdatingLastStoppedAt) listeners) => metric.Type switch {
        AvailabilityMetric.AvailabilityType.WORKING => TraceWorkingMetric(at: metric.TracedAt, onUpdatingLastStoppedAt: listeners.OnUpdatingLastStoppedAt),
        AvailabilityMetric.AvailabilityType.STOPPED => TraceStoppedMetric(at: metric.TracedAt, maybeStopReason: metric.MaybeDowntimeReason, idsGeneratorFn, 
                                                                          onAdding: listeners.OnAdding, onUpdatingLastStoppedAt: listeners.OnUpdatingLastStoppedAt),
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

    private Result TraceStoppedMetric(DateTime at, Maybe<string> maybeStopReason, Func<long> idsGeneratorFn, Action<long> onAdding, Action<long> onUpdatingLastStoppedAt)
    {
        var isStoppingMetricSetWithKnownReason = maybeStopReason.HasValue;
        if(isStoppingMetricSetWithKnownReason)
            return Result.Success();  // we are ONLY interested in unKnown stopping reasons, so that users set that reason in this system

        var maybeCurrentStoppedPeriod = FindCurrentStoppedPeriod();
        if(maybeCurrentStoppedPeriod.HasValue)
        {
            maybeCurrentStoppedPeriod.Value.TraceNewStopMetric(at);
            onUpdatingLastStoppedAt(maybeCurrentStoppedPeriod.Value.Id);
            return Result.Success();
        }

        var newPeriodId = idsGeneratorFn();
        onAdding(newPeriodId);
        _periods.Add(DowntimePeriod.For(stoppedAt: at, withId: newPeriodId));
        return Result.Success();
    }

    private Result TraceWorkingMetric(DateTime at, Action<long> onUpdatingLastStoppedAt)
    {
        var maybeCurrentStoppedPeriod = FindCurrentStoppedPeriod();
        var isWorkingMetricForAnAlreadyWorkingDevice = maybeCurrentStoppedPeriod.HasNoValue;
        if(isWorkingMetricForAnAlreadyWorkingDevice)
            return Result.Success();

        maybeCurrentStoppedPeriod.Value.Finish(at);
        onUpdatingLastStoppedAt(maybeCurrentStoppedPeriod.Value.Id);
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
