using System;
using System.Threading;
using System.Threading.Tasks;

using StopReasons.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace StopReasons.Jobs;

public class AvailabilityMetricsListener: IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private IServiceScope _serviceScope = null;
    private IMessageReceiver _messageReceiver = null;
    private AvailabilityStateManager _availabilityStateManager = null;

    public AvailabilityMetricsListener(IServiceProvider serviceProvider)
    {
        this._serviceProvider = serviceProvider;
    }

    public void Dispose()
    {
        _availabilityStateManager?.Dispose();
        _messageReceiver?.Dispose();
        _serviceScope?.Dispose();
    }

    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _serviceScope = this._serviceProvider.CreateScope();
            _messageReceiver = _serviceScope.ServiceProvider.GetRequiredService<IMessageReceiver>();
            _availabilityStateManager = _serviceScope.ServiceProvider.GetRequiredService<AvailabilityStateManager>();
            System.Console.WriteLine("Listening on availability messages ...");
            return _messageReceiver.StartReceivingMessages(async message => await _availabilityStateManager.Process(message));
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine($"[AvailabilityMetricsListener Job]: {ex.Message}");
            return Task.CompletedTask;
        }
    }

    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        this.Dispose();
        return Task.CompletedTask;
    }
}