using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace StopReasons.Services;

public sealed class IntegrationService: IDisposable
{
    private readonly IAvailabilityMetricStorage _persistence;
    private readonly AvailabilityStateManagerConfig _config;

    private long _currentIdUsedForDowntimePeriods = 0;

    private abstract record ActionToTakeWhenProcessingMetric;
    private sealed record NoAction : ActionToTakeWhenProcessingMetric;
    private sealed record StartNewStoppedPeriod : ActionToTakeWhenProcessingMetric;
    private sealed record ProlongCurrentStoppedPeriod(long CurrentStoppedPeriodId) : ActionToTakeWhenProcessingMetric;
    private sealed record FinishCurrentStoppedPeriod(long CurrentStoppedPeriodId) : ActionToTakeWhenProcessingMetric;

    public IntegrationService(IOptions<AvailabilityStateManagerConfig> config, IAvailabilityMetricStorage persistence)
    {
        this._config = config.Value;
        this._persistence = persistence;

        this._currentIdUsedForDowntimePeriods = GetFromPersistenceTheCurrentIdUsedForDowntimePeriods().Result;
        Console.WriteLine($"[IntegrationService] Most recent ID loaded was {_currentIdUsedForDowntimePeriods}");
    }

    public async Task ProcessIntegrationMessage(string message)
    {
        var metricParsedResult = AvailabilityMetric.From(message,
            withAllowedStates: (workingStateLabel: _config.WorkingStatusLabel, stoppedStateLabel: _config.StoppedStatusLabel));
        if (metricParsedResult.IsFailure)
        {
            Console.Error.WriteLine("[IntegrationService] Error parsing integration message" + metricParsedResult.Error);
            return;
        }

        var processingResult = await ProcessMetric(metric: metricParsedResult.Value);
        if (processingResult.IsFailure)
        {
            Console.Error.WriteLine("[IntegrationService] Error processing Metric" + processingResult.Error);
            return;
        }

        Console.WriteLine("[IntegrationService] " + metricParsedResult.Value);
    }

    private async Task<Result> ProcessMetric(AvailabilityMetric metric)
    {
        await PersistMetric(withMetric: metric,
            actionToTake: DetermineActionToTakeWhenProcessingNewReportedMetric(
                newAvailabilityTypeReportedNow: metric.Type,
                maybeIdOfMostRecentStoppedPeriod: await _persistence.GetDowntimePeriodIdOfMostRecentStoppedPeriod(forDeviceId: metric.DeviceId),
                isStoppingReasonKnown: metric.IsStoppingReasonKnown));

        return Result.Success();
    }

    private static ActionToTakeWhenProcessingMetric DetermineActionToTakeWhenProcessingNewReportedMetric(
        AvailabilityMetric.AvailabilityType newAvailabilityTypeReportedNow, Maybe<long> maybeIdOfMostRecentStoppedPeriod,
        Maybe<bool> isStoppingReasonKnown)
    {
        var deviceWasStillReportedAsStoppedByNow = maybeIdOfMostRecentStoppedPeriod.HasValue;
        if (deviceWasStillReportedAsStoppedByNow)
        {
            if (newAvailabilityTypeReportedNow == AvailabilityMetric.AvailabilityType.WORKING)
                return new FinishCurrentStoppedPeriod(CurrentStoppedPeriodId: maybeIdOfMostRecentStoppedPeriod.Value);

            if (newAvailabilityTypeReportedNow == AvailabilityMetric.AvailabilityType.STOPPED)
                return new ProlongCurrentStoppedPeriod(CurrentStoppedPeriodId: maybeIdOfMostRecentStoppedPeriod.Value);
        }
        else
        {
            if (newAvailabilityTypeReportedNow == AvailabilityMetric.AvailabilityType.STOPPED && false == isStoppingReasonKnown)
                return new StartNewStoppedPeriod();
        }

        return new NoAction();
    }

    private Task PersistMetric(AvailabilityMetric withMetric, ActionToTakeWhenProcessingMetric actionToTake)
    {
        return actionToTake switch
        {
            StartNewStoppedPeriod => _persistence.Add(new IAvailabilityMetricStorage.AvailabilityMetricInStorage(
                                        Id: ++this._currentIdUsedForDowntimePeriods, DeviceId: withMetric.DeviceId,
                                        InitiallyStoppedAt: withMetric.TracedAt, LastStoppedMetricTracedAt: withMetric.TracedAt)),

            ProlongCurrentStoppedPeriod info => _persistence.UpdateLastStoppedMetricTracedAt(id: info.CurrentStoppedPeriodId, at: withMetric.TracedAt, isItStillStopped: true),

            FinishCurrentStoppedPeriod info => _persistence.UpdateLastStoppedMetricTracedAt(id: info.CurrentStoppedPeriodId, at: withMetric.TracedAt, isItStillStopped: false),

            _ => Task.CompletedTask,
        };
    }

    private async Task<long> GetFromPersistenceTheCurrentIdUsedForDowntimePeriods()
    {
        return await _persistence.GetCurrentIdUsedForDowntimePeriods();
    }

    public void Dispose()
    {
        _persistence?.Dispose();
    }

}
