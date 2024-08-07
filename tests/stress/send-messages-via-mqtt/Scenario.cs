using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SendMessagesViaMqtt;

public sealed class Scenario
{
    public delegate void AddKeyboardListenerFn(ConsoleKey forKey, KeyboardService.HandleKeyPressedEventFn keyPressedHandler, string withMessage);
    public delegate void RemoveKeyboardListenerFn(ConsoleKey stopListeningEventsFromKey);

    private const string _AvailabilityMqttTopicName = "disponibilidad/principal";
    private const string _QualityMqttTopicName = "Calidad";

    public string Name { get; init; }
    public string ClientId { get; init; }
    public Device[] Devices { get; init; }
    public int MetricCountToSendPerDevice { get; init; }
    public DateTime StartingFromDate { get; init; }
    public bool ShouldItSendTimestamps { get; init; }
    public int MillisecondsToWaitWhileSendingEachMetric { get; init; }

    public AddKeyboardListenerFn AddKeyboardListener { get; init; }
    public RemoveKeyboardListenerFn RemoveKeyboardListener { get; init; }

    public async Task RunAsync(Random usingRandomizer, CancellationToken token)
    {
        try
        {
            Console.WriteLine($"\n------ Running Availability scenario '{Name}' ---------");

            await using var broker = new Broker();
            await broker.ConnectAsync(ClientId, token);

            await Task.WhenAll(Enumerable.Range(start: 1, count: Devices.Count())
                                         .Select(deviceId => SendMetricsAsync(deviceId, broker, usingRandomizer, token))
                            );
        }
        catch(Exception ex)
        {
            Console.WriteLine($"General exception caught. Reason: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private async Task SendMetricsAsync(int forDeviceId, Broker withBroker, Random usingRandomizer, CancellationToken token)
    {
        var bindingContext = BindKeyToDeviceId(forDeviceId, usingRandomizer);
        AddKeyboardListener(forKey: bindingContext.forKey, keyPressedHandler: bindingContext.keyPressedHandler, withMessage: bindingContext.withMessage);

        foreach(var metrics in Devices[forDeviceId - 1].GetMetrics(forDeviceId, ShouldItSendTimestamps, StartingFromDate).Take(MetricCountToSendPerDevice))
        {
            if(token.IsCancellationRequested)
                break;

            await withBroker.SendMessageAsync(targetBrokerTopic: _AvailabilityMqttTopicName, message: metrics.AvailabilityMetric, token: token);
            Console.WriteLine($"MQTT message published to '{_AvailabilityMqttTopicName}' topic with payload: '{metrics.AvailabilityMetric}'");

            await withBroker.SendMessageAsync(targetBrokerTopic: _QualityMqttTopicName, message: metrics.QualityMetric, token: token);
            Console.WriteLine($"MQTT message published to '{_QualityMqttTopicName}' topic with payload: '{metrics.QualityMetric}'");

            await Task.Delay(millisecondsDelay: MillisecondsToWaitWhileSendingEachMetric);
        }

        RemoveKeyboardListener(Devices[forDeviceId - 1].KeyToBind);
    }

    private (ConsoleKey forKey, KeyboardService.HandleKeyPressedEventFn keyPressedHandler, string withMessage) BindKeyToDeviceId(int forDeviceId, Random usingRandomizer) =>
        (
            forKey: Devices[forDeviceId - 1].KeyToBind,
            keyPressedHandler: keyPressedModifiers => {

                if(keyPressedModifiers.NoModifiersWerePressed())
                    Devices[forDeviceId - 1].ToggleAvailability(shouldSetKnownReasonWhenStopped: true, usingRandomizer);
                else if(keyPressedModifiers.WithCtrl)
                    Devices[forDeviceId - 1].ToggleAvailability(shouldSetKnownReasonWhenStopped: false, usingRandomizer);
                else if(keyPressedModifiers.WithShift)
                    Devices[forDeviceId - 1].IncrementRejectedCount();
                
                var stopRunningKeyPressedEvents = false;
                return stopRunningKeyPressedEvents;
            },
            withMessage: $"Press '{Devices[forDeviceId - 1].KeyToBind}' key to toggle availability for Device Id '{forDeviceId}'"
        );
}
