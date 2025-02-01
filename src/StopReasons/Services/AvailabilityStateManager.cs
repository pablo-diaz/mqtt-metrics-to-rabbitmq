using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using CSharpFunctionalExtensions;

namespace StopReasons.Services;

public class AvailabilityStateManager: IDisposable
{
    private readonly AvailabilityStateManagerConfig _config;
    private readonly IAvailabilityMetricStorage _persistence;
    private readonly Dictionary<string, DeviceDowntimePeriodsTracker> _stopsPerDevice = new();
    
    private long _currentIdUsedForDowntimePeriods = 0;
    private readonly Func<long> _idGeneratorForDowntimePeriods;

    private const int _hack_hoursInBogota = -5;

    public sealed record PeriodToReport(DateTimeOffset From, DateTimeOffset To);

    public AvailabilityStateManager(AvailabilityStateManagerConfig config, IAvailabilityMetricStorage persistence)
    {
        this._config = config;
        this._persistence = persistence;
        this.LoadPreExistingReasonsFromPersistence(fromDate: GetDateSinceWhenToLoadPreExistingReasonsFromPersistence(config.SinceWhenToLoadPreExistingReasonsFromPersistence));
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

    public IEnumerable<DowntimePeriodWithReasonSet> GetMostRecentDowntimeReasons(PeriodToReport inPeriod) =>
        _stopsPerDevice.Select(kv => (devId: kv.Key, periods: kv.Value.GetPeriodsWithReasonSet(from: GetLocalTime(inPeriod.From), to: GetLocalTime(inPeriod.To))))
                       .SelectMany(t => t.periods, (t, p) => p with { deviceId = t.devId });

    private static DateTime GetLocalTime(DateTimeOffset from) =>
        from.DateTime.AddHours(_hack_hoursInBogota);  // TODO: I'm being lazy and I will find a better way soon

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

    private static DateTime GetDateSinceWhenToLoadPreExistingReasonsFromPersistence(string from) => from switch {
        "1h" => DateTime.Now.AddHours(-1),
        "3h" => DateTime.Now.AddHours(-3),
        "12h" => DateTime.Now.AddHours(-12),
        "1d" => DateTime.Now.AddDays(-1),
        "3d" => DateTime.Now.AddDays(-3),
        "15d" => DateTime.Now.AddDays(-15),
        "1mo" => DateTime.Now.AddMonths(-1),

        _ => DateTime.Now.AddHours(-1),
    };

    private void LoadPreExistingReasonsFromPersistence(DateTime fromDate)
    {
        Console.WriteLine($"Loading PreExisting reasons from persistence, from {fromDate:yyyy-MM-dd HH:mm:ss}");

        foreach(var metric in Task.Run(async () => await this._persistence.Load(fromDate)).Result)
        {
            if(_currentIdUsedForDowntimePeriods < metric.Id)
                _currentIdUsedForDowntimePeriods = metric.Id;

            if(_stopsPerDevice.ContainsKey(metric.DeviceId) == false)
                _stopsPerDevice[metric.DeviceId] = new DeviceDowntimePeriodsTracker();

            _stopsPerDevice[metric.DeviceId].AddPeriod(fromPersistenceMetric: metric);
        }
    }

    public void Dispose()
    {
        _persistence?.Dispose();
    }
}
