using System.Threading.Tasks;

using BrokerConsumer.Infra;
using BrokerConsumer.Services;
using BrokerConsumer.Infra.DTOs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrokerConsumer;

public class Program
{
    public static async Task Main(string[] args)
    {
        await CreateHostBuilder(args).RunConsoleAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseConsoleLifetime()

            // TODO: set logging capabilities
            //.ConfigureLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            
            .ConfigureServices((hostContext, services) =>
            {
                var configBuilder = new ConfigurationBuilder()
                                        .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                                        .AddJsonFile(path: "appsettings.json", optional: false);
                
                var configuration = configBuilder.Build();

                services.AddSingleton<IMessageReceiver, RabbitMqMessageReceiver>(sp =>
                    new RabbitMqMessageReceiver(config: configuration.GetSection("RabbitMqConfig").Get<RabbitMqConfiguration>()));

                services.AddSingleton<IMessageProcessor, MessageProcessorForInfluxDb>(sp => new MessageProcessorForInfluxDb(
                    influxConfig: configuration.GetSection("InfluxDbSetup").Get<InfluxDbConfig>(),
                    processorConfig: configuration.GetSection("ProcessorConfig").Get<ProcessorConfig>() ));

                services.AddHostedService<Jobs.BrokerMessageConsumer>();
            });
}
