using System.Text.Json.Serialization;

namespace csgame.Models
{
    public class ZarinpalVerificationResponse
    {
        [JsonPropertyName("data")]
        public ZarinpalVerificationResponseData? Data { get; set; }

        [JsonPropertyName("errors")]
        public object? Errors { get; set; } // می‌تواند آرایه یا آبجکت خالی باشد

        // Helper property to check for success (status 100) or already verified (101)
        public bool IsSuccess() => Data?.Code == 100 || Data?.Code == 101;
        public bool IsAlreadyVerified() => Data?.Code == 101;
    }
}
