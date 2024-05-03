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
    private class TestingScenario
    {
        public string Name { get; set; }
        public string ClientId { get; set; }
        public int DeviceCount { get; set; }
        public int MetricCountPerDevice { get; set; }
        public DateTime StartingFromDate { get; set; }
        public Func<int> MillisecondsToWaitWhileSendingEachMessageFn { get; set; }
    }

    public static async Task Main(string[] args)
    {
        await RunTestingScenarios(
            new TestingScenario {
                Name = "Cold start",
                ClientId = "PLM001",
                DeviceCount = 1,
                MetricCountPerDevice = 1,
                StartingFromDate = new DateTime(2024, 5, 16, 15, 30, 45),
                MillisecondsToWaitWhileSendingEachMessageFn = () => 1_000
            },

            new TestingScenario {
                Name = "1 device sending 20 metrics",
                ClientId = "PLM001",
                DeviceCount = 1,
                MetricCountPerDevice = 20,
                StartingFromDate = new DateTime(2024, 5, 17, 15, 30, 45),
                MillisecondsToWaitWhileSendingEachMessageFn = () => 1_000
            },

            new TestingScenario {
                Name = "10 devices sending 30 metrics each",
                ClientId = "PLM002",
                DeviceCount = 10,
                MetricCountPerDevice = 30,
                StartingFromDate = new DateTime(2024, 5, 18, 15, 30, 45),
                MillisecondsToWaitWhileSendingEachMessageFn = () => 1_000
            }
        );
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

    private static async Task RunTestingScenarios(params TestingScenario[] scenarios)
    {
        var randomizer = new Random(Seed: 125785);
        var mqttBrokerTopic = "temperature/living_room";

        foreach(var scenario in scenarios)
        {
            try
            {
                Console.WriteLine($"\n------ Running scenario '{scenario.Name}' ---------");

                using var client = await ConnectToMqttBroker(withClientId: scenario.ClientId);
                Console.WriteLine("Connected to MQTT broker successfully");

                await Task.WhenAll(Enumerable.Range(start: 1, count: scenario.DeviceCount)
                                   .Select(deviceId => SendMetrics(forDeviceId: deviceId, withMqttClient: client, targetBrokerTopic: mqttBrokerTopic,
                                           withMetricCountPerDevice: scenario.MetricCountPerDevice, usingRandomizer: randomizer, fromDate: scenario.StartingFromDate,
                                           getMillisecondsToWaitWhileSendingEachMessage: scenario.MillisecondsToWaitWhileSendingEachMessageFn)));

                await client.DisconnectAsync();
                Console.WriteLine("Finished runnig scenario. MQTT client session has been disconnected from broker");
            }
            catch(Exception ex)
            {
                Console.WriteLine($"General exception caught. Reason: {ex.Message}");
            }
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
