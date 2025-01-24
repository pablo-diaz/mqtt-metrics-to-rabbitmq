using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SendMessagesViaMqtt;

internal class TestScenarioRunner
{
    public static async Task RunSpecificTestingScenarioAsync(KeyboardService keyboard, Random randomizer,
        ConsoleParametersParser.ParametersForSpecificScenario parameters, CancellationTokenSource tokenSource)
    {
        ConsoleKey[] availableKeys = [
            ConsoleKey.A, ConsoleKey.S, ConsoleKey.D, ConsoleKey.F, ConsoleKey.G, ConsoleKey.H, ConsoleKey.J, ConsoleKey.K, ConsoleKey.L,
            ConsoleKey.W, ConsoleKey.E, ConsoleKey.R, ConsoleKey.T, ConsoleKey.Y, ConsoleKey.U, ConsoleKey.I, ConsoleKey.O, ConsoleKey.P,
            ConsoleKey.Z, ConsoleKey.B, ConsoleKey.N, ConsoleKey.M];

        if (parameters.DeviceCount <= 0 || parameters.DeviceCount > availableKeys.Length)
        {
            Console.WriteLine($"Cannot run testing scenario because there are not enough keys to allocate to each device. You requested {parameters.DeviceCount} devices but there are only {availableKeys.Length} keys available");
            return;
        }

        await Task.WhenAll(
            keyboard.RunKeyboardListeners(),

            RunTestingScenarios(whenFinished: keyboard.StopKeyboardListener, randomizer, tokenSource.Token,
                new Scenario()
                {
                    Name = $"{parameters.DeviceCount} devices sending metrics every second, for an overall of {parameters.NumberOfMetricsPerDevice} metrics total per device",
                    ClientId = "PLC002",
                    Devices = Enumerable.Range(1, parameters.DeviceCount)
                              .Select(deviceNumber => new Device()
                              {
                                  KeyToBind = availableKeys[deviceNumber - 1],
                                  Velocity = parameters.VelocityPerMinuteForEachDevice,
                                  WorkingForProductId = parameters.WorkingForProductId,
                                  GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: parameters.VelocityPerMinuteForEachDevice, withRandomizer: randomizer)
                              })
                              .ToArray(),
                    MetricCountToSendPerDevice = parameters.NumberOfMetricsPerDevice,
                    StartingFromDate = DateTime.Now,
                    ShouldItSendTimestamps = false,
                    ShouldItRandomlySendFailingQualityMessages = parameters.ShouldItRandomlySendFailingQualityMessages,
                    TargetMqttServer = parameters.TargetMqttServer,
                    ShouldBeVerbose = parameters.ShouldBeVerbose,
                    MillisecondsToWaitWhileSendingEachMetric = 1_000,
                    AddKeyboardListener = keyboard.AddKeyboardListener,
                    RemoveKeyboardListener = keyboard.RemoveKeyboardListener
                }
            )
        );
    }

    public static async Task RunDefaultTestingScenariosAsync(KeyboardService keyboard, Random randomizer, CancellationTokenSource tokenSource)
    {
        await Task.WhenAll(
            keyboard.RunKeyboardListeners(),

            RunTestingScenarios(whenFinished: () => keyboard.StopKeyboardListener(), randomizer, tokenSource.Token,
                /*new Scenario() {
                    Name = "1 device sending metrics once every 5 seconds, for an overall of 60 metrics total",
                    ClientId = "PLC001",
                    Devices = new Device[] {
                        new Device() { KeyToBind = ConsoleKey.A, Velocity = 600, WorkingForProductId = "002", GetApprovedCount = () => randomizer.Next(minValue: 1, maxValue: 10) },
                    },
                    MetricCountToSendPerDevice = 60,
                    StartingFromDate = DateTime.Now,
                    ShouldItSendTimestamps = true,
                    MillisecondsToWaitWhileSendingEachMetric = 5_000,
                    AddKeyboardListener = (forKey, keyPressedHandler, withMessage) => keyboard.AddKeyboardListener(forKey: forKey, keyPressedHandler: keyPressedHandler, withMessage: withMessage),
                    RemoveKeyboardListener = key => keyboard.RemoveKeyboardListener(forKey: key)
                },*/

                new Scenario()
                {
                    Name = "3 devices sending metrics every second, for an overall of 240 metrics total per device",
                    ClientId = "PLC002",
                    Devices = new Device[] {
                        new Device() { KeyToBind = ConsoleKey.A, Velocity = 600, WorkingForProductId = "002", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.S, Velocity = 600, WorkingForProductId = "002", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.D, Velocity = 600, WorkingForProductId = "002", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) }
                    },
                    MetricCountToSendPerDevice = 240,
                    StartingFromDate = DateTime.Now,
                    ShouldItSendTimestamps = false,
                    ShouldBeVerbose = false,
                    ShouldItRandomlySendFailingQualityMessages = false,
                    TargetMqttServer = "localhost",
                    MillisecondsToWaitWhileSendingEachMetric = 1_000,
                    AddKeyboardListener = (forKey, keyPressedHandler, withMessage) => keyboard.AddKeyboardListener(forKey: forKey, keyPressedHandler: keyPressedHandler, withMessage: withMessage),
                    RemoveKeyboardListener = key => keyboard.RemoveKeyboardListener(forKey: key)
                }

                /*new Scenario() {
                    Name = "20 devices sending metrics every second, for an overall of 500 metrics total per device",
                    ClientId = "PLC003",
                    Devices = new Device[] {
                        new Device() { KeyToBind = ConsoleKey.A, Velocity = 600, WorkingForProductId = "005", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.S, Velocity = 600, WorkingForProductId = "005", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.D, Velocity = 600, WorkingForProductId = "005", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.F, Velocity = 600, WorkingForProductId = "005", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.G, Velocity = 600, WorkingForProductId = "005", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.H, Velocity = 600, WorkingForProductId = "005", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.J, Velocity = 600, WorkingForProductId = "005", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.K, Velocity = 600, WorkingForProductId = "006", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.L, Velocity = 600, WorkingForProductId = "006", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.Z, Velocity = 600, WorkingForProductId = "006", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.X, Velocity = 600, WorkingForProductId = "006", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.V, Velocity = 600, WorkingForProductId = "006", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.D, Velocity = 600, WorkingForProductId = "007", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.B, Velocity = 600, WorkingForProductId = "007", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.N, Velocity = 600, WorkingForProductId = "007", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.M, Velocity = 600, WorkingForProductId = "007", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.W, Velocity = 600, WorkingForProductId = "007", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.E, Velocity = 600, WorkingForProductId = "007", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.R, Velocity = 600, WorkingForProductId = "007", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) },
                        new Device() { KeyToBind = ConsoleKey.T, Velocity = 600, WorkingForProductId = "007", GetApprovedCount = () => GetApprovedCountForSecond(forVelocityPerMinute: 600, withRandomizer: randomizer) }
                    },
                    MetricCountToSendPerDevice = 500,
                    StartingFromDate = DateTime.Now,
                    ShouldItSendTimestamps = true,
                    MillisecondsToWaitWhileSendingEachMetric = 1_000,
                    AddKeyboardListener = (forKey, keyPressedHandler, withMessage) => keyboard.AddKeyboardListener(forKey: forKey, keyPressedHandler: keyPressedHandler, withMessage: withMessage),
                    RemoveKeyboardListener = key => keyboard.RemoveKeyboardListener(forKey: key)
                }*/
            )
        );
    }

    private static async Task RunTestingScenarios(Action whenFinished, Random withRandomizer, CancellationToken token, params Scenario[] scenarios)
    {
        foreach (var scenario in scenarios)
        {
            if (token.IsCancellationRequested)
                break;

            await scenario.RunAsync(withRandomizer, token);
        }

        whenFinished();
    }

    private static int GetApprovedCountForSecond(int forVelocityPerMinute, Random withRandomizer)
    {
        var velocityPerSecond = forVelocityPerMinute / 60;
        var amountToDecrementFromVelocity = withRandomizer.Next(minValue: 1, maxValue: velocityPerSecond / 2);
        var shouldDecrementApprovingCountFromVelocity = withRandomizer.Next(minValue: 1, maxValue: 1_000) > 950;

        return shouldDecrementApprovingCountFromVelocity
            ? velocityPerSecond - amountToDecrementFromVelocity
            : velocityPerSecond;
    }

}
