namespace Map.Models
{
    public sealed class WsControlMessage
    {
        public string Type { get; set; } = "control";
        public int Train { get; set; }
        public int Operation { get; set; }
        public int Value { get; set; }
        public string? CommandId { get; set; }
        public string Timestamp { get; set; } = "";
    }
}