using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SendMessagesViaMqtt;

public sealed class QualityScenario: ITestingScenario
{
    public string Name { get; init; }
    public string ClientId { get; init; }
    public int DeviceCount { get; init; }
    public int MetricCountPerDevice { get; init; }
    public DateTime StartingFromDate { get; init; }
    public Func<int> MillisecondsToWaitWhileSendingEachMessageFn { get; init; }

    private const string _TargetMqttTopicName = "Calidad";

    public async Task RunAsync(Random usingRandomizer, CancellationToken token)
    {
        try
        {
            Console.WriteLine($"\n------ Running Quality scenario '{Name}' ---------");

            await using var broker = new Broker();
            await broker.ConnectAsync(ClientId, token);

            await Task.WhenAll(Enumerable.Range(start: 1, count: DeviceCount)
                                         .Select(deviceId => SendMetricsAsync(deviceId, broker, usingRandomizer, token))
                              );
        }
        catch(Exception ex)
        {
            Console.WriteLine($"General exception caught. Reason: {ex.Message}");
        }
    }

    private async Task SendMetricsAsync(int forDeviceId, Broker withBroker, Random usingRandomizer, CancellationToken token)
    {
        foreach(var message in GetMetrics(forDeviceId, usingRandomizer).Take(MetricCountPerDevice))
        {
            if(token.IsCancellationRequested)
                break;

            await withBroker.SendMessageAsync(targetBrokerTopic: _TargetMqttTopicName, message: message, token: token);
            Console.WriteLine($"MQTT message published to '{_TargetMqttTopicName}' topic with payload: '{message}'");

            await Task.Delay(millisecondsDelay: MillisecondsToWaitWhileSendingEachMessageFn());
        }
    }

    private IEnumerable<string> GetMetrics(int forDeviceId, Random usingRandomizer)
    {
        var aDate = StartingFromDate;

        while(true)
        {
            var deviceId = "Dev" + forDeviceId.ToString().PadLeft(totalWidth: 10, paddingChar: '0');
            var deviceVelocity = 600;  // products this device can create in 60 seconds
            var deviceWorkingForProductId = "002";
            var approved = usingRandomizer.Next(minValue: 1, maxValue: 10);
            var rejected = usingRandomizer.Next(minValue: -200, maxValue: 10);
            var quiality = $"{deviceVelocity}@{deviceWorkingForProductId}@Aprobada@{approved}@Rechazada@{(rejected < 0 ? 0 : rejected)}";

            yield return $"{deviceId}@{quiality}@{aDate:yyyy-M-d@H_m_s}";

            aDate = aDate.AddSeconds(1);
        }
    }

}
