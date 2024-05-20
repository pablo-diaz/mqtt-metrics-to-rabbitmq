namespace StopReasons.Infra;

public class RabbitMqConfiguration
{
    public class MetricsConfig
    {
        public string Queue { get; set; }
        public int CompetingConsumersCount { get; set; }
    }

    public string ConnectionString { get; set; }
    public MetricsConfig AvailabilityMetricsConfig { get; set; }
}
