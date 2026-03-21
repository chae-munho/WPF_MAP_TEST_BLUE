namespace Map.Models
{
    public sealed class WsPositionMessage
    {
        public string Type { get; set; } = "";
        public int Train { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string Source { get; set; } = "";
        public string Timestamp { get; set; } = "";
    }
}