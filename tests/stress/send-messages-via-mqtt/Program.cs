using System;
using System.Threading;
using System.Threading.Tasks;

namespace SendMessagesViaMqtt;

public class Program
{
    public static async Task Main(string[] args)
    {
        var tokenSource = new CancellationTokenSource();
        var keyboard = new KeyboardService();

        keyboard.AddKeyboardListener(forKey: ConsoleKey.Q, withMessage: "Press 'Q' key to stop running test scenarios", callbackFn: (wasItPressedWithControlKey) => {
            tokenSource.Cancel();
            var stopRunningKeyPressedEvents = true;
            return stopRunningKeyPressedEvents;
        });

        await Task.WhenAll(
            keyboard.RunKeyboardListeners(),

            RunTestingScenarios(whenFinished: () => keyboard.StopKeyboardListener(), tokenSource.Token,
                /*new AvailabilityScenario() {
                    Name = "3 devices sending 200 availability metrics each",
                    ClientId = "PLC002",
                    DeviceCount = 3,
                    MetricCountPerDevice = 200,
                    StartingFromDate = DateTime.Now,
                    MillisecondsToWaitWhileSendingEachMessageFn = () => 1_000,
                    AddKeyboardListener = theParams => keyboard.AddKeyboardListener(forKey: theParams.forKey, callbackFn: theParams.callbackFn, withMessage: theParams.withMessage),
                    RemoveKeyboardListener = key => keyboard.RemoveKeyboardListener(forKey: key)
                }*/

                new QualityScenario() {
                    Name = "3 devices sending 20 quality metrics each",
                    ClientId = "PLC003",
                    DeviceCount = 3,
                    MetricCountPerDevice = 20,
                    StartingFromDate = DateTime.Now,
                    MillisecondsToWaitWhileSendingEachMessageFn = () => 1_000
                }
            )
        );

        Console.WriteLine("\n ************* Finished runnig all scenarios *********************");
    }
    
    private static async Task RunTestingScenarios(Action whenFinished, CancellationToken token, params ITestingScenario[] scenarios)
    {
        var randomizer = new Random(Seed: 125785);
        foreach(var scenario in scenarios)
        {
            if(token.IsCancellationRequested)
                break;

            await scenario.RunAsync(randomizer, token);
        }

        whenFinished();
    }
}
