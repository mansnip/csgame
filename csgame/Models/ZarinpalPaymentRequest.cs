using System.Text.Json.Serialization;

namespace csgame.Models
{
    public class ZarinpalPaymentRequest
    {
        [JsonPropertyName("merchant_id")]
        public required string MerchantId { get; set; }

        [JsonPropertyName("amount")]
        public int Amount { get; set; } // به ریال

        [JsonPropertyName("description")]
        public required string Description { get; set; }

        [JsonPropertyName("callback_url")]
        public required string CallbackUrl { get; set; }

        [JsonPropertyName("metadata")]
        // ***** نوع اینجا باید تغییر کند *****
        public List<ZarinpalMetadata>? Metadata { get; set; } // به جای ZarinpalMetadata?
    }
}
