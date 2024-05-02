using System;
using System.Threading.Tasks;

namespace BrokerConsumer.Services;

public interface IMessageProcessor: IDisposable
{
    Task Process(string message);
}
