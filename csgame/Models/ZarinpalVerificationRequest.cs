using System.Text.Json.Serialization;

namespace csgame.Models
{
    public class ZarinpalVerificationRequest
    {
        [JsonPropertyName("merchant_id")]
        public required string MerchantId { get; set; }

        [JsonPropertyName("amount")]
        public int Amount { get; set; } // به ریال

        [JsonPropertyName("authority")]
        public required string Authority { get; set; }
    }
}
