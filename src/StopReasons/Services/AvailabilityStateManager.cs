using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using CSharpFunctionalExtensions;

namespace StopReasons.Services;

public class AvailabilityStateManager
{
    private readonly AvailabilityStateManagerConfig _config;
    private readonly Dictionary<string, DeviceDowntimePeriodsTracker> _stopsPerDevice = new(); // TODO: persist state
    
    private long _currentIdUsedForDowntimePeriods = 0; // TODO: persist current ID seq
    private readonly Func<long> _idGeneratorForDowntimePeriods;

    public AvailabilityStateManager(AvailabilityStateManagerConfig config)
    {
        this._config = config;
        this._idGeneratorForDowntimePeriods = () => {
            _currentIdUsedForDowntimePeriods++;
            return _currentIdUsedForDowntimePeriods;
        };
    }

    public Task Process(string message)
    {
        var messageParsedResult = AvailabilityMetric.From(message,
            withAllowedStates: (workingStateLabel: _config.WorkingStatusLabel, stoppedStateLabel: _config.StoppedStatusLabel));
        if(messageParsedResult.IsFailure)
        {
            System.Console.WriteLine("[AvailabilityStateManager] " + messageParsedResult.Error);
            return Task.CompletedTask;
        }

        var processingResult = Process(metric: messageParsedResult.Value);
        if(processingResult.IsFailure)
        {
            System.Console.WriteLine("[AvailabilityStateManager] " + processingResult.Error);
            return Task.CompletedTask;
        }

        System.Console.WriteLine("[AvailabilityStateManager] Message was processed successfully: " + messageParsedResult.Value);
        return Task.CompletedTask;
    }

    public Result SetDowntimeReason(string reason, string forDeviceId, long forDowntimePeriodId)
    {
        if(_stopsPerDevice.ContainsKey(forDeviceId) == false)
            return Result.Failure($"Device id '{forDeviceId}' was not found");

        return _stopsPerDevice[forDeviceId].SetDowntimeReason(reason: reason, forDowntimePeriodId: forDowntimePeriodId);
    }

    public IEnumerable<PendingDowntimePeriodToSetReasonsFor> GetPendingDowntimePeriodsToSetReasonsFor() =>
        _stopsPerDevice.Select(kv => (devId: kv.Key, pendings: kv.Value.GetPendingDowntimePeriodsToSetReasonsFor()))
                       .SelectMany(t => t.pendings, (t, p) => p with { deviceId = t.devId });

    public IEnumerable<DowntimePeriodWithReasonSet> GetMostRecentDowntimePeriods() =>
        _stopsPerDevice.Select(kv => (devId: kv.Key, periods: kv.Value.GetPeriodsWithReasonSet()))
                       .SelectMany(t => t.periods, (t, p) => p with { deviceId = t.devId });
    
    private Result Process(AvailabilityMetric metric)
    {
        if(_stopsPerDevice.ContainsKey(metric.DeviceId) == false)
            _stopsPerDevice[metric.DeviceId] = new DeviceDowntimePeriodsTracker();

        return _stopsPerDevice[metric.DeviceId].Process(metric, idsGeneratorFn: _idGeneratorForDowntimePeriods);
    }
}
