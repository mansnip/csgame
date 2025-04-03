using csgame.Models;

namespace csgame.Services // یا namespace پروژه شما
{
    public interface IZarinpalService
    {
        Task<ZarinpalPaymentResponse> RequestPaymentAsync(int amount, string description, string callbackUrl, string? email = null, string? mobile = null);
        Task<ZarinpalVerificationResponse> VerifyPaymentAsync(int amount, string authority);
    }
}