using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using CSharpFunctionalExtensions;

namespace StopReasons.Services;

public class AvailabilityStateManager
{
    private readonly AvailabilityStateManagerConfig _config;
    private readonly IAvailabilityMetricStorage _persistence;
    private readonly Dictionary<string, DeviceDowntimePeriodsTracker> _stopsPerDevice = new();
    
    private long _currentIdUsedForDowntimePeriods = 0;
    private readonly Func<long> _idGeneratorForDowntimePeriods;

    public AvailabilityStateManager(AvailabilityStateManagerConfig config, IAvailabilityMetricStorage persistence)
    {
        this._config = config;
        this._persistence = persistence;
        this.LoadMetricsFromPersistence();
        this._idGeneratorForDowntimePeriods = () => {
            _currentIdUsedForDowntimePeriods++;
            return _currentIdUsedForDowntimePeriods;
        };
    }

    public async Task Process(string message)
    {
        var messageParsedResult = AvailabilityMetric.From(message,
            withAllowedStates: (workingStateLabel: _config.WorkingStatusLabel, stoppedStateLabel: _config.StoppedStatusLabel));
        if(messageParsedResult.IsFailure)
        {
            System.Console.WriteLine("[AvailabilityStateManager] " + messageParsedResult.Error);
            return;
        }

        var processingResult = await Process(metric: messageParsedResult.Value);
        if(processingResult.IsFailure)
        {
            System.Console.WriteLine("[AvailabilityStateManager] " + processingResult.Error);
            return;
        }

        System.Console.WriteLine("[AvailabilityStateManager] Message was processed successfully: " + messageParsedResult.Value);
    }

    public async Task<Result> SetDowntimeReason(string reason, string forDeviceId, long forDowntimePeriodId)
    {
        if(_stopsPerDevice.ContainsKey(forDeviceId) == false)
            return Result.Failure($"Device id '{forDeviceId}' was not found");

        await _persistence.StoreReason(id: forDowntimePeriodId, reason: reason);

        return _stopsPerDevice[forDeviceId].SetDowntimeReason(reason: reason, forDowntimePeriodId: forDowntimePeriodId);
    }

    public IEnumerable<PendingDowntimePeriodToSetReasonsFor> GetPendingDowntimePeriodsToSetReasonsFor() =>
        _stopsPerDevice.Select(kv => (devId: kv.Key, pendings: kv.Value.GetPendingDowntimePeriodsToSetReasonsFor()))
                       .SelectMany(t => t.pendings, (t, p) => p with { deviceId = t.devId });

    public IEnumerable<DowntimePeriodWithReasonSet> GetMostRecentDowntimePeriods() =>
        _stopsPerDevice.Select(kv => (devId: kv.Key, periods: kv.Value.GetPeriodsWithReasonSet()))
                       .SelectMany(t => t.periods, (t, p) => p with { deviceId = t.devId });
    
    private async Task<Result> Process(AvailabilityMetric metric)
    {
        if(_stopsPerDevice.ContainsKey(metric.DeviceId) == false)
            _stopsPerDevice[metric.DeviceId] = new DeviceDowntimePeriodsTracker();

        var shouldUpdateLastStoppedAt = false;
        var shouldAddNewRecord = false;
        var periodIdInContext = 0L;

        var result = _stopsPerDevice[metric.DeviceId].Process(metric, idsGeneratorFn: _idGeneratorForDowntimePeriods,
                        listeners: (
                            OnAdding: newPeriodId => {
                                shouldAddNewRecord = true;
                                periodIdInContext = newPeriodId;
                            },
                            
                            OnUpdatingLastStoppedAt: forPeriodId => {
                                shouldUpdateLastStoppedAt = true;
                                periodIdInContext = forPeriodId;
                            }
                        )
                    );

        if(result.IsFailure)
            return result;

        await PersistChanges(forPeriodId: periodIdInContext, withMetric: metric, actionToPerform: (ShouldUpdateLastStoppedAt: shouldUpdateLastStoppedAt, ShouldAddNewRecord: shouldAddNewRecord));

        return Result.Success();
    }

    private Task PersistChanges(long forPeriodId, AvailabilityMetric withMetric, (bool ShouldUpdateLastStoppedAt, bool ShouldAddNewRecord) actionToPerform)
    {
        if(actionToPerform.ShouldAddNewRecord)
            return _persistence.Add(new IAvailabilityMetricStorage.AvailabilityMetricInStorage(
                        Id: forPeriodId, DeviceId: withMetric.DeviceId, InitiallyStoppedAt: withMetric.TracedAt, LastStoppedMetricTracedAt: withMetric.TracedAt,
                        MaybeReason: withMetric.MaybeDowntimeReason));

        if(actionToPerform.ShouldUpdateLastStoppedAt)
            return _persistence.UpdateLastStoppedMetricTracedAt(id: forPeriodId, at: withMetric.TracedAt);

        return Task.CompletedTask;
    }

    private void LoadMetricsFromPersistence()
    {
        foreach(var metric in Task.Run(async () => await this._persistence.Load()).Result)
        {
            if(_currentIdUsedForDowntimePeriods < metric.Id)
                _currentIdUsedForDowntimePeriods = metric.Id;

            if(_stopsPerDevice.ContainsKey(metric.DeviceId) == false)
                _stopsPerDevice[metric.DeviceId] = new DeviceDowntimePeriodsTracker();

            _stopsPerDevice[metric.DeviceId].AddPeriod(fromPersistenceMetric: metric);
        }
    }
}
