namespace Map.Models
{
    public sealed class WsVideoStopMessage
    {
        public string Type { get; set; } = "";
        public int Train { get; set; }
        public string? RequestId { get; set; }
        public string Timestamp { get; set; } = "";
    }
} 