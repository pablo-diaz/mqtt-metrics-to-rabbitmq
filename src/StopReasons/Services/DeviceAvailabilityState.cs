using System;
using System.Linq;
using System.Collections.Generic;
using CSharpFunctionalExtensions;

namespace StopReasons.Services;

public class DowntimePeriod
{
    public DateTime InitiallyStoppedAt { get; }
    public DateTime LastStoppedMetricTracedAt { get; private set; }
    public bool IsItStillStopped { get; private set; }
    public Maybe<string> MaybeReason { get; private set; } = Maybe<string>.None;

    private DowntimePeriod(DateTime initiallyStoppedAt, DateTime lastStoppedMetricTracedAt, bool isItStillStopped)
    {
        InitiallyStoppedAt = initiallyStoppedAt;
        LastStoppedMetricTracedAt = lastStoppedMetricTracedAt;
        IsItStillStopped = isItStillStopped;
    }

    public static DowntimePeriod For(DateTime stoppedAt) =>
        new DowntimePeriod(initiallyStoppedAt: stoppedAt, lastStoppedMetricTracedAt: stoppedAt, isItStillStopped: false);

    public void TraceNewStopMetric(DateTime forDate)
    {
        LastStoppedMetricTracedAt = forDate;
    }

    public void Finish(DateTime at)
    {
        LastStoppedMetricTracedAt = at;
        IsItStillStopped = true;
    }

    public void SetReason(string reason)
    {
        this.MaybeReason = reason;
    }
}

public class DeviceDowntimePeriodsTracker
{
    private List<DowntimePeriod> _periods = new();

    public void Process(AvailabilityMetric metric)
    {
        if(metric.Type == AvailabilityMetric.AvailabilityType.STOPPED)
        {
            TraceStoppedMetric(at: metric.TracedAt);
            return;
        }

        TraceWorkingMetric(at: metric.TracedAt);
    }

    private void TraceStoppedMetric(DateTime at)
    {
        var maybeCurrentStoppedPeriod = _periods.FirstOrDefault(p => p.IsItStillStopped) ?? Maybe<DowntimePeriod>.None;
        if(maybeCurrentStoppedPeriod.HasValue)
        {
            maybeCurrentStoppedPeriod.Value.TraceNewStopMetric(at);
            return;
        }

        _periods.Add(DowntimePeriod.For(stoppedAt: at));
    }

    private void TraceWorkingMetric(DateTime at)
    {
        var maybeCurrentStoppedPeriod = _periods.FirstOrDefault(p => p.IsItStillStopped) ?? Maybe<DowntimePeriod>.None;
        if(maybeCurrentStoppedPeriod.HasNoValue)
            return;

        maybeCurrentStoppedPeriod.Value.Finish(at);
    }
}
