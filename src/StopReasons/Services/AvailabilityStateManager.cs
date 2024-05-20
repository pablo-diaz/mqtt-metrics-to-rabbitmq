using System.Threading.Tasks;
using System.Collections.Generic;

namespace StopReasons.Services;

public class AvailabilityStateManager
{
    private readonly AvailabilityStateManagerConfig _config;
    private readonly Dictionary<string, DeviceDowntimePeriodsTracker> _stopsPerDevice = new();

    public AvailabilityStateManager(AvailabilityStateManagerConfig config)
    {
        this._config = config;
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

        Process(metric: messageParsedResult.Value);

        System.Console.WriteLine("[AvailabilityStateManager] Message was received: " + messageParsedResult.Value);
        return Task.CompletedTask;
    }

    private void Process(AvailabilityMetric metric)
    {
        if(_stopsPerDevice.ContainsKey(metric.DeviceId) == false)
            _stopsPerDevice[metric.DeviceId] = new DeviceDowntimePeriodsTracker();

        _stopsPerDevice[metric.DeviceId].Process(metric);
    }
}
