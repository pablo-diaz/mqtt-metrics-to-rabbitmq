using System;
using System.Threading;
using System.Threading.Tasks;

using BrokerConsumer.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace BrokerConsumer.Jobs;

public class BrokerMessageConsumer: IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private IServiceScope _serviceScope = null;
    private IMessageReceiver _messageReceiver = null;
    private IMessageProcessor _messageProcessor = null;

    public BrokerMessageConsumer(IServiceProvider serviceProvider)
    {
        this._serviceProvider = serviceProvider;
    }

    public void Dispose()
    {
        _messageProcessor?.Dispose();
        _messageReceiver?.Dispose();
        _serviceScope?.Dispose();
    }

    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _serviceScope = this._serviceProvider.CreateScope();
            _messageReceiver = _serviceScope.ServiceProvider.GetRequiredService<BrokerConsumer.Services.IMessageReceiver>();
            _messageProcessor = _serviceScope.ServiceProvider.GetRequiredService<BrokerConsumer.Services.IMessageProcessor>();
            System.Console.WriteLine("Listening on broker messages ...");
            return _messageReceiver.StartReceivingMessages(message => _messageProcessor.Process(message));
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine($"[BrokerMessageConsumer Job]: {ex.Message}");
            return Task.CompletedTask;
        }
    }

    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        this.Dispose();
        return Task.CompletedTask;
    }
}