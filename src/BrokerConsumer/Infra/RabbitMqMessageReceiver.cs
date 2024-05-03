using System;
using System.Text.Json;
using System.Threading.Tasks;

using BrokerConsumer.Services;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BrokerConsumer.Infra;

// https://www.rabbitmq.com/dotnet-api-guide.html
public sealed class RabbitMqMessageReceiver : IMessageReceiver
{
    private readonly RabbitMqConfiguration _config;
    private readonly IConnection _conn;
    private readonly IModel _channel;

    public RabbitMqMessageReceiver(RabbitMqConfiguration config)
    {
        this._config = config;

        var factory = new ConnectionFactory();
        factory.Uri = new Uri(_config.ConnectionString);
        factory.DispatchConsumersAsync = true;
        factory.ConsumerDispatchConcurrency = _config.TemperatureMetricsConfig.CompetingConsumersCount;

        _conn = factory.CreateConnection();
        _channel = _conn.CreateModel();
    }

    public void Dispose()
    {
        if(_channel != null && _channel.IsOpen)
            _channel.Dispose();

        if(_conn != null && _conn.IsOpen)
            _conn.Dispose();
    }

    public Task StartReceivingMessages(Func<string, Task> messageHandlerAsyncFn)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, queueMessage) => {
            try
            {
                await messageHandlerAsyncFn(System.Text.Encoding.UTF8.GetString(queueMessage.Body.ToArray()));
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"General exception caught while processing broker message. Reason: {ex.Message}");
                throw;
            }

            _channel.BasicAck(deliveryTag: queueMessage.DeliveryTag, multiple: false);
        };
        _channel.BasicConsume(queue: _config.TemperatureMetricsConfig.Queue, autoAck: false, consumer: consumer);
        return Task.CompletedTask;
    }
}
