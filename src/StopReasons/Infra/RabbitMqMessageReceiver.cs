using System;
using System.Threading.Tasks;

using StopReasons.Services;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Microsoft.Extensions.Options;

namespace StopReasons.Infra;

// https://www.rabbitmq.com/dotnet-api-guide.html
public sealed class RabbitMqMessageReceiver : IMessageReceiver
{
    private readonly RabbitMqConfiguration _config;
    private readonly IConnection _conn;
    private readonly IModel _channel;

    public RabbitMqMessageReceiver(IOptions<RabbitMqConfiguration> config)
    {
        this._config = config.Value;

        var factory = new ConnectionFactory();
        factory.Uri = new Uri(_config.ConnectionString);
        factory.DispatchConsumersAsync = true;
        factory.ConsumerDispatchConcurrency = _config.AvailabilityMetricsConfig.CompetingConsumersCount;

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
                _channel.BasicAck(deliveryTag: queueMessage.DeliveryTag, multiple: false);
                await messageHandlerAsyncFn(System.Text.Encoding.UTF8.GetString(queueMessage.Body.ToArray()));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"General exception caught while processing broker message. Reason: {ex.Message}");
                throw;
            }

        };

        consumer.Registered += (_, information) => {
            Console.WriteLine("[Message from RabbitMQ] Registered message receibed");
            return Task.CompletedTask;
        };

        consumer.Shutdown += (_, information) => {
            Console.WriteLine("[Message from RabbitMQ] Shutdown message receibed");
            return Task.CompletedTask;
        };

        consumer.Unregistered += (_, information) => {
            Console.WriteLine("[Message from RabbitMQ] Unregistered message receibed");
            return Task.CompletedTask;
        };

        _channel.BasicConsume(queue: _config.AvailabilityMetricsConfig.Queue, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

}
