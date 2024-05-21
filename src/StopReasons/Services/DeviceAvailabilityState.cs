using System;

using CSharpFunctionalExtensions;

namespace StopReasons.Services;

public class DowntimePeriod
{
    public long Id { get; }
    public DateTime InitiallyStoppedAt { get; }
    public DateTime LastStoppedMetricTracedAt { get; private set; }
    public bool IsItStillStopped { get; private set; }
    public Maybe<string> MaybeReason { get; private set; } = Maybe<string>.None;

    private DowntimePeriod(long id, DateTime initiallyStoppedAt, DateTime lastStoppedMetricTracedAt, bool isItStillStopped)
    {
        Id = id;
        InitiallyStoppedAt = initiallyStoppedAt;
        LastStoppedMetricTracedAt = lastStoppedMetricTracedAt;
        IsItStillStopped = isItStillStopped;
    }

    public static DowntimePeriod For(DateTime stoppedAt, Func<long> getNextIdFn) =>
        new DowntimePeriod(id: getNextIdFn(), initiallyStoppedAt: stoppedAt, lastStoppedMetricTracedAt: stoppedAt, isItStillStopped: true);

    public void TraceNewStopMetric(DateTime forDate)
    {
        LastStoppedMetricTracedAt = forDate;
    }

    public void Finish(DateTime at)
    {
        LastStoppedMetricTracedAt = at;
        IsItStillStopped = false;
    }

    public void SetReason(string reason)
    {
        this.MaybeReason = reason;
    }
}
