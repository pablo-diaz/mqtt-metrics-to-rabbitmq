namespace BrokerConsumer.Infra;

public sealed class InfluxDbConfig
{
    public string ServiceUrl { get; set; }
    public string ServiceToken { get; set; }
    public string Bucket { get; set; }
    public string Organization { get; set; }
    public string TargetMeasurement { get; set; }
}