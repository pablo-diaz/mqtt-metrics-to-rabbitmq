using System.Threading.Tasks;

using BrokerConsumer.Services;

namespace BrokerConsumer.Infra;

public class MessageProcessorForInfluxDb: IMessageProcessor
{
    public Task Process(string message)
    {
        System.Console.WriteLine($"Message received from broker: '{message}'");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // TODO: complete here
    }
}
