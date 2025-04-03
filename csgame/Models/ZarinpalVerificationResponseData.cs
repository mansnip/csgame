using System.Text.Json.Serialization;

namespace csgame.Models
{
    public class ZarinpalVerificationResponseData
    {
        [JsonPropertyName("code")]
        public int Code { get; set; } // 100: Success, 101: Already Verified

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("card_hash")]
        public string? CardHash { get; set; } // هش کارت

        [JsonPropertyName("card_pan")]
        public string? CardPan { get; set; } // شماره کارت ماسک شده

        [JsonPropertyName("ref_id")]
        public long? RefId { get; set; } // شماره پیگیری تراکنش

        [JsonPropertyName("fee_type")]
        public string? FeeType { get; set; }

        [JsonPropertyName("fee")]
        public int Fee { get; set; }
    }
}
