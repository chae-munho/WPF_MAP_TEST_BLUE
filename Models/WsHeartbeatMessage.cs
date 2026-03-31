namespace Map.Models
{
    public sealed class WsHeartbeatMessage
    {
        public int Train { get; set; }
        public string Type { get; set; } = "";
        public string Timestamp { get; set; } = "";
    }
}