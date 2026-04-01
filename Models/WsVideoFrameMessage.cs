namespace Map.Models
{
    public sealed class WsVideoFrameMessage
    {
        public string Type { get; set; } = "";
        public int Train { get; set; }
        public int CarNo { get; set; }
        public string ImageBase64 { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public string Format { get; set; } = "";
        public string Timestamp { get; set; } = "";
    }
}