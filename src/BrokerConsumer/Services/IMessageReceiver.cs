using System;
using System.Threading.Tasks;

namespace BrokerConsumer.Services;

public interface IMessageReceiver: IDisposable
{
    Task StartReceivingMessages(Func<string, Task> messageHandlerAsyncFn);
}
