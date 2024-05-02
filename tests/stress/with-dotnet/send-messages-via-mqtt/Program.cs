using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace SendMessagesViaMqtt;

public class Program
{
    public static async Task Main(string[] args)
    {
        await RunTestingScenarios();
    }

    private static async Task<IMqttClient> ConnectToMqttBroker(string withClientId)
    {
        var mqttFactory = new MqttFactory();
        var mqttClient = mqttFactory.CreateMqttClient();
        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithClientId(withClientId)
            .WithTcpServer(server: "localhost")
            .WithCredentials(username: "mqtt-enabled-user", password: "prueba2024")
            .Build();

        await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
        return mqttClient;
    }

    private static async Task SendMetrics(int forDeviceId, IMqttClient withMqttClient, string targetBrokerTopic, int withMetricCountPerDevice, Random usingRandomizer, DateTime fromDate, Func<int> getMillisecondsToWaitWhileSendingEachMessage)
    {
        foreach(var message in GetTemperatureMetrics(forDeviceId: forDeviceId, usingRandomizer, fromDate)
                               .Take(withMetricCountPerDevice))
        {
            var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(targetBrokerTopic)
                .WithPayload(message)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await withMqttClient.PublishAsync(applicationMessage, CancellationToken.None);
            Console.WriteLine($"MQTT message published to '{targetBrokerTopic}' topic with payload: '{message}'");

            await Task.Delay(millisecondsDelay: getMillisecondsToWaitWhileSendingEachMessage());
        }
    }

    private static Task RunScenario(string scenarioName, IMqttClient withMqttClient, string targetBrokerTopic, int forDeviceCount, int withMetricCountPerDevice, Random usingRandomizer, DateTime fromDate, Func<int> getMillisecondsToWaitWhileSendingEachMessage)
    {
        Console.WriteLine($"Running scenario '{scenarioName}'");
        var tasks = Enumerable.Range(start: 1, count: forDeviceCount)
                    .Select(deviceId => SendMetrics(forDeviceId: deviceId, withMqttClient: withMqttClient, targetBrokerTopic: targetBrokerTopic, withMetricCountPerDevice: withMetricCountPerDevice,
                        usingRandomizer: usingRandomizer, fromDate: fromDate, getMillisecondsToWaitWhileSendingEachMessage: getMillisecondsToWaitWhileSendingEachMessage));

        return Task.WhenAll(tasks);
    }

    private static async Task RunTestingScenarios()
    {
        try
        {
            using var client = await ConnectToMqttBroker(withClientId: "PLC001");
            Console.WriteLine("Connected to MQTT broker successfully");

            var randomizer = new Random(Seed: 125785);

            await RunScenario(scenarioName: "Sending fifty temperatures from one device, every second", withMqttClient: client, targetBrokerTopic: "temperature/living_room",
                forDeviceCount: 1, withMetricCountPerDevice: 50, usingRandomizer: randomizer, fromDate: new DateTime(2024, 5, 17, 20, 55, 40),
                getMillisecondsToWaitWhileSendingEachMessage: () => 1_000);

            await RunScenario(scenarioName: "Sending twenty temperatures from ten devices, every second", withMqttClient: client, targetBrokerTopic: "temperature/living_room",
                forDeviceCount: 10, withMetricCountPerDevice: 20, usingRandomizer: randomizer, fromDate: new DateTime(2024, 5, 17, 23, 59, 55),
                getMillisecondsToWaitWhileSendingEachMessage: () => 1_000);

            await client.DisconnectAsync();
            Console.WriteLine("Finished: MQTT client session has been disconnected from broker");
        }
        catch(Exception ex)
        {
            Console.WriteLine($"General exception caught. Reason: {ex.Message}");
        }
    }

    private static IEnumerable<string> GetTemperatureMetrics(int forDeviceId, Random usingRandomizer, DateTime fromDate)
    {
        var aDate = fromDate;

        while(true)
        {
            var deviceId = "Dev" + forDeviceId.ToString().PadLeft(totalWidth: 10, paddingChar: '0');
            var temperature = Math.Round(value: usingRandomizer.NextDouble() * 100.0, digits: 2);

            yield return $"{deviceId}@{temperature}@{aDate:yyyy-M-d@H_m_s}";

            aDate = aDate.AddSeconds(1);
        }
    }
}
