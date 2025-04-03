using csgame.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace csgame.Services // یا namespace پروژه شما
{
    public class ZarinpalService : IZarinpalService
    {
        private readonly HttpClient _httpClient;
        private readonly ZarinpalSettings _settings;
        private readonly ILogger<ZarinpalService> _logger;

        public ZarinpalService(IHttpClientFactory httpClientFactory, IOptions<ZarinpalSettings> settings, ILogger<ZarinpalService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("ZarinpalClient");
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<ZarinpalPaymentResponse> RequestPaymentAsync(int amount, string description, string callbackUrl, string? email = null, string? mobile = null)
        {
            var requestUrl = _settings.GetRequestUrl();
            _logger.LogInformation("Requesting payment from Zarinpal. Amount: {Amount}, Callback: {Callback}", amount, callbackUrl);
            _logger.LogInformation("Sending Zarinpal payment request to URL: {Url}", requestUrl);

            ZarinpalMetadata? currentMetadata = null;
            if (email != null || mobile != null /* || orderId != null */) // اگر order_id را اضافه کردید، اینجا هم بررسی کنید
            {
                currentMetadata = new ZarinpalMetadata
                {
                    Email = email,
                    Mobile = mobile,
                    // OrderId = orderId // اگر order_id را اضافه کردید
                };
            }

            var requestModel = new ZarinpalPaymentRequest
            {
                MerchantId = _settings.MerchantId,
                Amount = amount, // Ensure amount is in Rials if required by API v4
                Description = description,
                CallbackUrl = callbackUrl,
                Metadata = (currentMetadata != null)
                    ? new List<ZarinpalMetadata> { currentMetadata } // لیست حاوی یک آیتم
                    : new List<ZarinpalMetadata>() // ارسال لیست خالی به جای null 
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(requestUrl, requestModel);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<ZarinpalPaymentResponse>();
                    if (result != null)
                    {
                        if (result.IsSuccess() && result.Data?.Authority != null)
                        {
                            _logger.LogInformation("Zarinpal payment request successful. Authority: {Authority}", result.Data.Authority);
                        }
                        else
                        {
                            _logger.LogWarning("Zarinpal payment request returned non-success code or empty authority. Code: {Code}, Message: {Message}, Errors: {Errors}", result.Data?.Code, result.Data?.Message, JsonSerializer.Serialize(result.Errors));
                        }
                        return result;
                    }
                    else
                    {
                        _logger.LogError("Failed to deserialize Zarinpal payment request response. Status Code: {StatusCode}", response.StatusCode);
                        return new ZarinpalPaymentResponse { Errors = new { Code = -99, Message = "Failed to deserialize response" } }; // Custom error
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Zarinpal payment request failed. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, errorContent);
                    // Attempt to deserialize error if possible (Zarinpal error structure varies)
                    try
                    {
                        var errorResult = JsonSerializer.Deserialize<ZarinpalPaymentResponse>(errorContent);
                        if (errorResult != null) return errorResult;
                    }
                    catch { /* Ignore deserialization error, return generic error */ }

                    return new ZarinpalPaymentResponse { Errors = new { Code = (int)response.StatusCode, Message = "HTTP request failed" } }; // Custom error
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception during Zarinpal payment request.");
                return new ZarinpalPaymentResponse { Errors = new { Code = -98, Message = $"Network error: {ex.Message}" } }; // Custom error
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception during Zarinpal payment request.");
                return new ZarinpalPaymentResponse { Errors = new { Code = -97, Message = $"Unexpected error: {ex.Message}" } }; // Custom error
            }
        }

        public async Task<ZarinpalVerificationResponse> VerifyPaymentAsync(int amount, string authority)
        {
            var verifyUrl = _settings.GetVerifyUrl();
            _logger.LogInformation("Verifying payment with Zarinpal. Authority: {Authority}, Amount: {Amount}", authority, amount);

            var requestModel = new ZarinpalVerificationRequest
            {
                MerchantId = _settings.MerchantId,
                Amount = amount, // Ensure amount matches the original request amount in Rials
                Authority = authority
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(verifyUrl, requestModel);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<ZarinpalVerificationResponse>();
                    if (result != null)
                    {
                        if (result.IsSuccess())
                        {
                            _logger.LogInformation("Zarinpal payment verification successful. RefID: {RefID}, Code: {Code}", result.Data?.RefId, result.Data?.Code);
                        }
                        else
                        {
                            _logger.LogWarning("Zarinpal payment verification returned non-success code. Code: {Code}, Message: {Message}, Errors: {Errors}", result.Data?.Code, result.Data?.Message, JsonSerializer.Serialize(result.Errors));
                        }
                        return result;
                    }
                    else
                    {
                        _logger.LogError("Failed to deserialize Zarinpal verification response. Status Code: {StatusCode}", response.StatusCode);
                        return new ZarinpalVerificationResponse { Errors = new { Code = -99, Message = "Failed to deserialize response" } }; // Custom error
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Zarinpal verification request failed. Status Code: {StatusCode}, Response: {Response}", response.StatusCode, errorContent);
                    // Attempt to deserialize error if possible
                    try
                    {
                        var errorResult = JsonSerializer.Deserialize<ZarinpalVerificationResponse>(errorContent);
                        if (errorResult != null) return errorResult;
                    }
                    catch { /* Ignore deserialization error, return generic error */ }

                    return new ZarinpalVerificationResponse { Errors = new { Code = (int)response.StatusCode, Message = "HTTP request failed" } }; // Custom error
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception during Zarinpal verification.");
                return new ZarinpalVerificationResponse { Errors = new { Code = -98, Message = $"Network error: {ex.Message}" } }; // Custom error
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception during Zarinpal verification.");
                return new ZarinpalVerificationResponse { Errors = new { Code = -97, Message = $"Unexpected error: {ex.Message}" } }; // Custom error
            }
        }
    }
}