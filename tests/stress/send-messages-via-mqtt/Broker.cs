using System;
using System.Threading;
using System.Threading.Tasks;

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace SendMessagesViaMqtt;

public sealed class Broker: IAsyncDisposable
{
    private IMqttClient _client;

    public Task ConnectAsync(string clientId, CancellationToken token)
    {
        var mqttFactory = new MqttFactory();
        _client = mqttFactory.CreateMqttClient();
        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(server: "localhost")
            .WithCredentials(username: "mqtt-enabled-user", password: "prueba2024")
            .Build();

        return _client.ConnectAsync(mqttClientOptions, token);
    }

    public Task SendMessageAsync(string targetBrokerTopic, string message, CancellationToken token)
    {
        var applicationMessage = new MqttApplicationMessageBuilder()
            .WithTopic(targetBrokerTopic)
            .WithPayload(message)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        return _client.PublishAsync(applicationMessage, token);
    }

    public async ValueTask DisposeAsync()
    {
        await _client?.DisconnectAsync();
    }
}