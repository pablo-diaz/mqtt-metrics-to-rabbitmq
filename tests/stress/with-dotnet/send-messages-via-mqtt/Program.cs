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
        public enum ScenarioType { Availability, Quality, Temperature }

        public ScenarioType Type { get; set; }
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
                Type = TestingScenario.ScenarioType.Temperature,
                Name = "3 devices sending 20 temperature metrics each",
                ClientId = "PLC001",
                DeviceCount = 3,
                MetricCountPerDevice = 20,
                StartingFromDate = DateTime.Now,
                MillisecondsToWaitWhileSendingEachMessageFn = () => 1_000
            },
            
            new TestingScenario {
                Type = TestingScenario.ScenarioType.Availability,
                Name = "3 devices sending 20 availability metrics each",
                ClientId = "PLC002",
                DeviceCount = 3,
                MetricCountPerDevice = 20,
                StartingFromDate = DateTime.Now,
                MillisecondsToWaitWhileSendingEachMessageFn = () => 1_000
            },

            new TestingScenario {
                Type = TestingScenario.ScenarioType.Quality,
                Name = "3 devices sending 20 quality metrics each",
                ClientId = "PLC003",
                DeviceCount = 3,
                MetricCountPerDevice = 20,
                StartingFromDate = DateTime.Now,
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

    private static async Task SendMetrics(int forDeviceId, Guid sessionId, IMqttClient withMqttClient, int withMetricCountPerDevice,
        Random usingRandomizer, DateTime fromDate, TestingScenario.ScenarioType scenarioType, Func<int> getMillisecondsToWaitWhileSendingEachMessage)
    {
        var messagesToSend = scenarioType switch {
            TestingScenario.ScenarioType.Temperature => GetTemperatureMetrics(forDeviceId: forDeviceId, sessionId, usingRandomizer, fromDate).Take(withMetricCountPerDevice),
            TestingScenario.ScenarioType.Availability => GetAvailabilityMetrics(forDeviceId: forDeviceId, sessionId, usingRandomizer, fromDate).Take(withMetricCountPerDevice),
            TestingScenario.ScenarioType.Quality => GetQualityMetrics(forDeviceId: forDeviceId, sessionId, usingRandomizer, fromDate).Take(withMetricCountPerDevice),
            _ => throw new Exception("Non expected scenario type")
        };

        var targetBrokerTopic = scenarioType switch {
            TestingScenario.ScenarioType.Temperature => "temperature/living_room",
            TestingScenario.ScenarioType.Quality => "Calidad",
            TestingScenario.ScenarioType.Availability => "Disponibilidad",
            _ => throw new Exception("Non expected scenario type 2")
        };

        foreach(var message in messagesToSend)
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
        foreach(var scenario in scenarios)
        {
            try
            {
                Console.WriteLine($"\n------ Running scenario '{scenario.Name}' ---------");

                using var client = await ConnectToMqttBroker(withClientId: scenario.ClientId);
                Console.WriteLine("Connected to MQTT broker successfully");

                var sessionId = Guid.NewGuid();

                await Task.WhenAll(Enumerable.Range(start: 1, count: scenario.DeviceCount)
                                   .Select(deviceId => SendMetrics(forDeviceId: deviceId, sessionId: sessionId, withMqttClient: client, scenarioType: scenario.Type,
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

        Console.WriteLine("\n ************* Finished runnig all scenarios *********************");
    }

    private static IEnumerable<string> GetTemperatureMetrics(int forDeviceId, Guid sessionId, Random usingRandomizer, DateTime fromDate)
    {
        var aDate = fromDate;
        var temperature = Math.Round(value: usingRandomizer.NextDouble() * 100.0, digits: 2);

        while(true)
        {
            var deviceId = "Dev" + forDeviceId.ToString().PadLeft(totalWidth: 10, paddingChar: '0');
            temperature = Math.Round(value: RandomlyGetNextTemperature(basedOnCurrentTemperature: temperature, randomizer: usingRandomizer), digits: 2);

            yield return $"{deviceId}@{temperature}@{aDate:yyyy-M-d@H_m_s}";

            aDate = aDate.AddSeconds(1);
        }
    }

    private static IEnumerable<string> GetAvailabilityMetrics(int forDeviceId, Guid sessionId, Random usingRandomizer, DateTime fromDate)
    {
        var aDate = fromDate;
        var isItAvailable = true;

        while(true)
        {
            var deviceId = "Dev" + forDeviceId.ToString().PadLeft(totalWidth: 10, paddingChar: '0');
            isItAvailable = usingRandomizer.Next(minValue: 1, maxValue: 300) >= 280 ? !isItAvailable : isItAvailable;
            var availability = isItAvailable ? "Produciendo" : "Parado";

            yield return $"{deviceId}@{availability}@{aDate:yyyy-M-d@H_m_s}";

            aDate = aDate.AddSeconds(1);
        }
    }

    private static IEnumerable<string> GetQualityMetrics(int forDeviceId, Guid sessionId, Random usingRandomizer, DateTime fromDate)
    {
        var aDate = fromDate;

        while(true)
        {
            var deviceId = "Dev" + forDeviceId.ToString().PadLeft(totalWidth: 10, paddingChar: '0');
            var approved = usingRandomizer.Next(minValue: 100, maxValue: 300);
            var rejected = usingRandomizer.Next(minValue: -200, maxValue: 10);
            var quiality = $"Aprobada@{approved}@Rechazada@{(rejected < 0 ? 0 : rejected)}";

            yield return $"{deviceId}@{quiality}@{aDate:yyyy-M-d@H_m_s}";

            aDate = aDate.AddSeconds(1);
        }
    }

    private static double RandomlyGetNextTemperature(double basedOnCurrentTemperature, Random randomizer)
    {
        var randomNumber = randomizer.Next(minValue: 1, maxValue: 200);
        var shouldIncreaseTemperature = randomNumber > 190;
        var shouldDecreaseTemperature = randomNumber < 10;
        var delta = randomizer.NextDouble() * 5.0;

        if(shouldIncreaseTemperature)
            return basedOnCurrentTemperature + delta;
        
        if(shouldDecreaseTemperature)
            return basedOnCurrentTemperature - delta;

        return basedOnCurrentTemperature;
    }
}
