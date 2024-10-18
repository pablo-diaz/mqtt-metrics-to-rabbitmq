using System;
using System.Threading;
using System.Threading.Tasks;

namespace SendMessagesViaMqtt;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var tokenSource = new CancellationTokenSource();
        var keyboard = new KeyboardService();
        var randomizer = new Random(Seed: 125785);

        keyboard.AddKeyboardListener(forKey: ConsoleKey.Escape, withMessage: "Press 'Esc' key to stop running test scenarios", keyPressedHandler: _ => {
            tokenSource.Cancel();
            const bool stopRunningKeyPressedEvents = true;
            return stopRunningKeyPressedEvents;
        });

        var maybeSpecificScenarioToRun = ConsoleParametersParser.GetParametersForSpecificScenario(fromConsoleArguments: args);
        if (maybeSpecificScenarioToRun == null)
            await TestScenarioRunner.RunDefaultTestingScenariosAsync(keyboard, randomizer, tokenSource);
        else
            await TestScenarioRunner.RunSpecificTestingScenarioAsync(keyboard, randomizer, maybeSpecificScenarioToRun.Value, tokenSource);

        Console.WriteLine("\n ************* Finished runnig all scenarios *********************");
    }
}
