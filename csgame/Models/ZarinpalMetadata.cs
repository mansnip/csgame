using System.Text.Json.Serialization;

namespace csgame.Models
{
    public class ZarinpalMetadata
    {
        [JsonPropertyName("mobile")]
        public string? Mobile { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }


    }
}
