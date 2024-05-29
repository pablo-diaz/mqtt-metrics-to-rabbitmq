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

    private DowntimePeriod(long id, DateTime initiallyStoppedAt, DateTime lastStoppedMetricTracedAt, bool isItStillStopped, Maybe<string> maybeReason)
    {
        Id = id;
        InitiallyStoppedAt = initiallyStoppedAt;
        LastStoppedMetricTracedAt = lastStoppedMetricTracedAt;
        IsItStillStopped = isItStillStopped;
        MaybeReason = maybeReason;
    }

    public static DowntimePeriod For(DateTime stoppedAt, long withId) =>
        new DowntimePeriod(id: withId, initiallyStoppedAt: stoppedAt, lastStoppedMetricTracedAt: stoppedAt, isItStillStopped: true, maybeReason: Maybe<string>.None);

    public static DowntimePeriod For(IAvailabilityMetricStorage.AvailabilityMetricInStorage fromPersistenceMetric) =>
        new DowntimePeriod(id: fromPersistenceMetric.Id, initiallyStoppedAt: fromPersistenceMetric.InitiallyStoppedAt,
                           lastStoppedMetricTracedAt: fromPersistenceMetric.LastStoppedMetricTracedAt,
                           isItStillStopped: false, maybeReason: fromPersistenceMetric.MaybeReason);

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
