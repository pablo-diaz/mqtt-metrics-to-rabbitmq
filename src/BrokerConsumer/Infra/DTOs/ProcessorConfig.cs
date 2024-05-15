namespace BrokerConsumer.Infra.DTOs;

public class ProcessorConfig
{
    public class Part
    {
        public string Name { get; set; }
        public string Purpose { get; set; }
        public string Type { get; set; }
        public bool Skip { get; set; } = false;
    }

    public string DeviceInfoFilePath { get; set; }
    public Part[] MessageParts { get; set; }
}
