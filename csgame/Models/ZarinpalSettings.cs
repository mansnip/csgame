namespace csgame.Models
{
    public class ZarinpalSettings
    {
        public required string MerchantId { get; set; }
        public bool UseSandbox { get; set; }
        public required string CallbackUrlBase { get; set; }

        public string GetRequestUrl() => UseSandbox
            ? "https://sandbox.zarinpal.com/pg/v4/payment/request.json"
            : "https://payment.zarinpal.com/pg/v4/payment/request.json"; // V4 API

        public string GetVerifyUrl() => UseSandbox
            ? "https://sandbox.zarinpal.com/pg/v4/payment/verify.json"
            : "https://payment.zarinpal.com/pg/v4/payment/verify.json"; // V4 API

        public string GetGatewayUrl(string authority) => UseSandbox
            ? $"https://sandbox.zarinpal.com/pg/StartPay/{authority}"
            : $"https://payment.zarinpal.com/pg/StartPay/{authority}";
    }
}
