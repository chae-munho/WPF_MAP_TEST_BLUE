namespace Map.Models
{
    public sealed class WsHelloMessage
    {
        public string Type { get; set; } = "";
        public string Role { get; set; } = "";
        public string ClientName { get; set; } = "";
        public string Timestamp { get; set; } = "";
    }
}