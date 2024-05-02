namespace BrokerConsumer.Infra;

public class RabbitMqConfiguration
{
    public class MetricsConfig
    {
        public string Exchange { get; set; }
        public string Queue { get; set; }
        public string RoutingKey { get; set; }
        public int CompetingConsumersCount { get; set; }
    }

    public string ConnectionString { get; set; }
    public MetricsConfig TemperatureMetricsConfig { get; set; }
}
