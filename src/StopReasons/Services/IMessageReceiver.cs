using System;
using System.Threading.Tasks;

namespace StopReasons.Services;

public interface IMessageReceiver: IDisposable
{
    Task StartReceivingMessages(Func<string, Task> messageHandlerAsyncFn);
}
