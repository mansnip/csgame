using System.Text.Json.Serialization;

namespace csgame.Models
{
    public class ZarinpalPaymentResponse
    {
        [JsonPropertyName("data")]
        public ZarinpalPaymentResponseData? Data { get; set; }

        [JsonPropertyName("errors")]
        public object? Errors { get; set; } // می‌تواند آرایه یا آبجکت خالی باشد

        // Helper property to check for success based on common Zarinpal v4 usage
        public bool IsSuccess() => (Data?.Code == 100 && !string.IsNullOrEmpty(Data.Authority));
    }
}
