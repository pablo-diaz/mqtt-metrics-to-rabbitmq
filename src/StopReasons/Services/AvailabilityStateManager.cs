using System.Threading.Tasks;

namespace StopReasons.Services;

public class AvailabilityStateManager
{
    private readonly AvailabilityStateManagerConfig _config;

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

        System.Console.WriteLine("[AvailabilityStateManager] Message was received: " + messageParsedResult.Value);
        return Task.CompletedTask;
    }
}
