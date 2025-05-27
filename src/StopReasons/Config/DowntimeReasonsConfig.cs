namespace StopReasons.Config;

public class DowntimeReasonsConfig
{
    public class ReasonInfo
    {
        public string Text { get; set; }    
        public string Code { get; set; }    
    }

    public ReasonInfo[] AllowedReasons { get; set; }

}
