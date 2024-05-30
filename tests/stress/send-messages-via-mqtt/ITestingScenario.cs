using System;
using System.Threading;
using System.Threading.Tasks;

namespace SendMessagesViaMqtt;

public interface ITestingScenario
{
    Task RunAsync(Random usingRandomizer, CancellationToken token);
}
