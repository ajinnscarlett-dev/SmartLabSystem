using System.Text.Json.Serialization;

namespace SmartLab.Shared
{
    public sealed class RemoteMouseCommandPayload
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("button")]
        public string? Button { get; set; }
    }

    public sealed class RemoteKeyCommandPayload
    {
        [JsonPropertyName("keyCode")]
        public int KeyCode { get; set; }
    }
}
