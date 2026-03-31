namespace Map.Models
{
    public sealed class WsHelloMessage
    {
        public int Train { get; set; }
        public string Type { get; set; } = "";
        public string Role { get; set; } = "";
        public string ClientName { get; set; } = "";
        public string Timestamp { get; set; } = "";
    }
}