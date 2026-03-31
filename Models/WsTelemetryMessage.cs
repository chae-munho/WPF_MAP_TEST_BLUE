namespace Map.Models
{
    public sealed class WsTelemetryMessage
    {
        public int Train { get; set; }
        public string Type { get; set; } = "";
        public int[] Data { get; set; } = System.Array.Empty<int>();
        public string Timestamp { get; set; } = "";
    }
}