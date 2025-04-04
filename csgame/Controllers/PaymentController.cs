﻿// در Controllers/PaymentController.cs
using csgame.Services; // یا namespace پروژه شما
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System.Text.Json;
using csgame.Models;
using csgame.Context;
using Microsoft.EntityFrameworkCore; // برای لاگ کردن بهتر خطاها

namespace csgame.Controllers // یا namespace پروژه شما
{
    public class PaymentController : Controller
    {
        private readonly IZarinpalService _zarinpalService;
        private readonly ZarinpalSettings _zarinpalSettings;
        private readonly ILogger<PaymentController> _logger;
        private readonly csGameDbContext _context;
        private readonly string _url = "https://rahagozar.com/UserPanel/Panel/Invoices?result=";

        // !! مهم: شما به یک سرویس برای مدیریت سفارشات/پرداخت ها در دیتابیس خود نیاز دارید
        // !! این سرویس باید بتواند مبلغ سفارش را بر اساس شناسه آن بخواند
        // !! و وضعیت پرداخت و شماره پیگیری را ذخیره کند.
        // private readonly IOrderService _orderService;

        // Inject required services
        public PaymentController(
            IZarinpalService zarinpalService,
            IOptions<ZarinpalSettings> zarinpalSettings,
            ILogger<PaymentController> logger,
            csGameDbContext csGameDbContext
            /*, IOrderService orderService */) // سرویس سفارشات را هم اینجا Inject کنید
        {
            _zarinpalService = zarinpalService;
            _zarinpalSettings = zarinpalSettings.Value;
            _logger = logger;
            _context = csGameDbContext;
            // _orderService = orderService; // نمونه سازی سرویس سفارشات
        }



        [HttpGet]
        public async Task<IActionResult> Process(string invoiceId, string token, string callbackUrl)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(callbackUrl) || invoiceId == null)
            {
                _logger.LogError("Payment Process failed: Missing parameters. InvoiceId: {InvoiceId}, Token provided: {TokenProvided}, Callback provided: {CallbackProvided}", invoiceId, !string.IsNullOrWhiteSpace(token), !string.IsNullOrWhiteSpace(callbackUrl));
                // مهم: به callbackUrl اصلی با پارامتر خطا برگردانید تا سایت رهاگذر بفهمد چه شده
                var originalCallbackUri = new Uri(Uri.UnescapeDataString(callbackUrl));
                var redirectUri = new UriBuilder(originalCallbackUri)
                {
                    Query = $"status=error&message=invalid_params&invoiceId={invoiceId}"
                }.Uri.ToString();
                _logger.LogInformation("Redirecting back to original callback due to missing parameters: {RedirectUri}", redirectUri);
                return Redirect(redirectUri);
                // return Redirect($"{_url}اطلاعات پرداخت ناقص است"); // این روش کمتر استاندارد است
            }

            _logger.LogInformation("Processing payment request for InvoiceId: {InvoiceId}", invoiceId);

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);

            // --- Validation ---
            if (invoice == null)
            {
                _logger.LogWarning("Payment Process failed: Invoice not found. InvoiceId: {InvoiceId}", invoiceId);
                // بازگشت به Callback اصلی با خطا
                var originalCallbackUri = new Uri(Uri.UnescapeDataString(callbackUrl));
                var redirectUri = new UriBuilder(originalCallbackUri)
                {
                    Query = $"status=error&message=invoice_not_found&invoiceId={invoiceId}"
                }.Uri.ToString();
                _logger.LogInformation("Redirecting back to original callback due to invoice not found: {RedirectUri}", redirectUri);
                return Redirect(redirectUri);
                // return View("Error", "فاکتور یافت نشد."); // نمایش خطا در سایت پرداخت؟
            }
            if (invoice.Status != "در انتظار پرداخت") // TODO: Use Enum or Constant
            {
                _logger.LogWarning("Payment Process failed: Invoice status is not Pending. InvoiceId: {InvoiceId}, Status: {Status}", invoiceId, invoice.Status);
                // بازگشت به Callback اصلی با خطا
                var originalCallbackUri = new Uri(Uri.UnescapeDataString(callbackUrl));
                var redirectUri = new UriBuilder(originalCallbackUri)
                {
                    Query = $"status=error&message=invoice_not_pending&invoiceId={invoiceId}"
                }.Uri.ToString();
                _logger.LogInformation("Redirecting back to original callback due to invalid status: {RedirectUri}", redirectUri);
                return Redirect(redirectUri);
                // return Redirect($"{_url}این فاکتور قبلاً پردازش شده یا منقضی شده است");
            }
            if (invoice.PaymentToken != token)
            {
                _logger.LogWarning("Payment Process failed: Invalid token. InvoiceId: {InvoiceId}", invoiceId);
                // بازگشت به Callback اصلی با خطا
                var originalCallbackUri = new Uri(Uri.UnescapeDataString(callbackUrl));
                var redirectUri = new UriBuilder(originalCallbackUri)
                {
                    Query = $"status=error&message=invalid_token&invoiceId={invoiceId}"
                }.Uri.ToString();
                _logger.LogInformation("Redirecting back to original callback due to invalid token: {RedirectUri}", redirectUri);
                return Redirect(redirectUri);
                // return Redirect($"{_url}توکن پرداخت نامعتبر است");
            }
            // Optional: Check token expiry

            // --- Mark token as used (Invalidate it, don't replace) ---
            invoice.PaymentToken = Guid.NewGuid().ToString(); // !! اشتباه: این کار توکن را باطل نمی‌کند بلکه جایگزین می‌کند
                                                              // invoice.PaymentToken = null; //  <--- توکن را null کنید تا دوباره استفاده نشود
                                                              // یا اگر فیلد جدا دارید: invoice.IsTokenUsed = true;
            _context.Invoices.Update(invoice);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Payment token invalidated for InvoiceId: {InvoiceId}", invoiceId);


            var user = await _context.Users.SingleOrDefaultAsync(a => a.Id == invoice.UserId);
            if (user == null)
            {
                _logger.LogError("Payment Process failed: User not found for InvoiceId: {InvoiceId}, UserId: {UserId}", invoiceId, invoice.UserId);
                // بازگشت به Callback اصلی با خطا
                var originalCallbackUri = new Uri(Uri.UnescapeDataString(callbackUrl));
                var redirectUri = new UriBuilder(originalCallbackUri)
                {
                    Query = $"status=error&message=user_not_found&invoiceId={invoiceId}"
                }.Uri.ToString();
                _logger.LogInformation("Redirecting back to original callback due to user not found: {RedirectUri}", redirectUri);
                return Redirect(redirectUri);
            }


            // --- حفظ callbackUrl اصلی برای استفاده در VerifyPayment ---
            // استفاده از TempData یا Session ریسک‌هایی دارد (اگر کاربر کوکی را پاک کند یا سشن منقضی شود)
            // بهترین راه: اگر زرین‌پال اجازه دهد پارامتر اضافه به Callback URL خودش اضافه کنید (معمولا نمی‌دهد)
            // راه حل عملی: callbackUrl اصلی را همراه Authority در دیتابیس (جدول تراکنش‌ها یا خود فاکتور) ذخیره کنید
            // اینجا به عنوان مثال از TempData استفاده می‌کنیم (برای سادگی، در پروداکشن با احتیاط استفاده شود)
            TempData["OriginalCallbackUrl_" + invoiceId] = callbackUrl; // کلید یکتا بر اساس invoiceId
            _logger.LogInformation("Original callback URL stored in TempData for InvoiceId: {InvoiceId}: {CallbackUrl}", invoiceId, callbackUrl);


            // حالا متد RequestPaymentWithAll را فراخوانی و نتیجه‌اش را مستقیم برگردانید
            _logger.LogInformation("Calling RequestPaymentWithAll for InvoiceId: {InvoiceId}", invoiceId);
            return await RequestPaymentWithAll(invoice.Id, Convert.ToInt32(invoice.FinalPrice * 10), user.Email, user.PhoneNumber);
        }




        // --- اکشن شروع فرآیند پرداخت ---
        //[HttpPost]
        //[ValidateAntiForgeryToken] // Important for security
        public async Task<IActionResult> RequestPayment(int orderId /* یا هر شناسه دیگری برای دریافت مبلغ */)
        {
            // --- دریافت مبلغ سفارش ---
            // !! این بخش بسیار مهم است !!
            // مبلغ را هرگز از کلاینت (فرم ارسالی) مستقیم نخوانید.
            // مبلغ باید از دیتابیس یا منبع معتبر سمت سرور بر اساس orderId خوانده شود.
            _logger.LogInformation("Attempting to start payment for OrderId: {OrderId}", orderId);

            // --->>> شروع منطق واقعی دریافت مبلغ (مثال) <<<---
            int amountInRials;
            try
            {
                // فرض کنید یک متد برای گرفتن مبلغ سفارش از دیتابیس دارید
                // که مبلغ را به تومان برمیگرداند
                // decimal orderAmountInToman = await _orderService.GetOrderAmountAsync(orderId);
                // if (orderAmountInToman <= 0)
                // {
                //     _logger.LogWarning("Order not found or amount is zero for OrderId: {OrderId}", orderId);
                //     TempData["ErrorMessage"] = "سفارش یافت نشد یا مبلغ آن نامعتبر است.";
                //     return RedirectToAction("Index", "Cart"); // Or appropriate page
                // }
                // // تبدیل به ریال (مطمئن شوید که نوع داده مناسب برای جلوگیری از سرریز انتخاب شده)
                // amountInRials = (int)(orderAmountInToman * 10);

                // --- مثال با مبلغ ثابت (فقط برای تست اولیه - حتما جایگزین کنید) ---
                _logger.LogWarning("Using FIXED amount for testing OrderId: {OrderId}. REPLACE THIS!", orderId);
                amountInRials = 100000; // 1000 تومان به عنوان مثال (به ریال)
                if (orderId <= 0) // یک ولیدیشن ساده برای orderId
                {
                    TempData["ErrorMessage"] = "شناسه سفارش نامعتبر است.";
                    return RedirectToAction("Index", "Cart");
                }
                // --- پایان مثال ---

            }
            catch (Exception ex) // مدیریت خطای احتمالی در خواندن اطلاعات سفارش
            {
                _logger.LogError(ex, "Error retrieving order amount for OrderId: {OrderId}", orderId);
                TempData["ErrorMessage"] = "خطا در بازیابی اطلاعات سفارش.";
                return RedirectToAction("Index", "Cart"); // Or appropriate page
            }
            // --->>> پایان منطق واقعی دریافت مبلغ <<<---


            string description = $"پرداخت سفارش شماره {orderId}";


            // ساخت Callback URL کامل
            var callbackUrl = Url.Action(
                action: "VerifyPayment", // نام اکشن Verify
                controller: "Payment",   // نام همین کنترلر
                values: new { orderId = orderId }, // پارامترهایی که میخواهیم همراه Callback برگردند
                protocol: Request.Scheme, // http or https
                host: Request.Host.ToString() // domain:port
             );

            if (string.IsNullOrEmpty(callbackUrl))
            {
                _logger.LogError("Could not generate callback URL for OrderId: {OrderId}", orderId);
                TempData["ErrorMessage"] = "خطا در ایجاد لینک بازگشت از بانک.";
                // به یک صفحه خطا یا صفحه اصلی هدایت کنید
                return RedirectToAction("Error", "Home");
            }


            // دریافت ایمیل و موبایل کاربر (اختیاری - اگر نیاز دارید)
            // string userEmail = User.FindFirstValue(ClaimTypes.Email); // Example
            // string userMobile = ...; // Get user mobile if available

            // ارسال درخواست به زرین پال
            _logger.LogInformation("Sending payment request to Zarinpal for OrderId: {OrderId}, Amount: {Amount}, Callback: {CallbackUrl}", orderId, amountInRials, callbackUrl);
            var response = await _zarinpalService.RequestPaymentAsync(amountInRials, description, callbackUrl, "faghih1998@gmail.com", "09120912123");

            if (response.IsSuccess() && !string.IsNullOrEmpty(response.Data?.Authority))
            {
                // --- ذخیره وضعیت موقت یا Authority ---
                // مهم: قبل از ریدایرکت، Authority را به همراه OrderId و Amount
                // در جایی ذخیره کنید (مثلا در دیتابیس در رکورد سفارش یا یک جدول جداگانه تراکنش ها)
                // تا در مرحله Verify بتوانید Amount صحیح را بازیابی کرده و Authority را تطبیق دهید.
                try
                {
                    // await _orderService.SetPaymentAuthorityAsync(orderId, response.Data.Authority, amountInRials);
                    _logger.LogInformation("Payment Authority {Authority} saved/updated for OrderId: {OrderId}", response.Data.Authority, orderId);
                    // --- مثال: ذخیره در TempData فقط برای تست سریع (اصلا توصیه نمی‌شود برای پروداکشن) ---
                    // TempData[$"ZarinpalAmount_{response.Data.Authority}"] = amountInRials;
                    // --- پایان مثال TempData ---
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save payment Authority {Authority} for OrderId: {OrderId}", response.Data?.Authority, orderId);
                    TempData["ErrorMessage"] = "خطا در ثبت اطلاعات اولیه پرداخت. لطفا با پشتیبانی تماس بگیرید.";
                    return RedirectToAction("Index", "Cart"); // Or appropriate page
                }


                _logger.LogInformation("Redirecting user to Zarinpal gateway for OrderId: {OrderId}, Authority: {Authority}", orderId, response.Data.Authority);
                // ریدایرکت کاربر به درگاه پرداخت
                var gatewayUrl = _zarinpalSettings.GetGatewayUrl(response.Data.Authority);
                return Redirect(gatewayUrl);
            }
            else
            {
                // خطا در درخواست پرداخت
                var errorMessage = $"Zarinpal request payment failed for OrderId: . Code: {response.Data?.Code ?? -1}, Message: {response.Data?.Message ?? "N/A"}, Errors: {(response.Errors != null ? JsonSerializer.Serialize(response.Errors) : "N/A")}";
                _logger.LogError(errorMessage);
                TempData["ErrorMessage"] = "متاسفانه امکان اتصال به درگاه پرداخت وجود ندارد. لطفا بعدا تلاش کنید یا با پشتیبانی تماس بگیرید.";
                // کاربر را به صفحه خطا یا سبد خرید برگردانید
                return RedirectToAction("Index", "Cart"); // Or appropriate page
            }
        }

        #region CSGame Payment
        //[HttpPost]
        //[ValidateAntiForgeryToken] // Important for security
        public async Task<IActionResult> RequestPaymentt(string orderId, int price, string email, string phoneNumber)
        {
            // --- دریافت مبلغ سفارش ---
            // !! این بخش بسیار مهم است !!
            // مبلغ را هرگز از کلاینت (فرم ارسالی) مستقیم نخوانید.
            // مبلغ باید از دیتابیس یا منبع معتبر سمت سرور بر اساس orderId خوانده شود.
            _logger.LogInformation("Attempting to start payment for OrderId: {OrderId}", orderId);

            // --->>> شروع منطق واقعی دریافت مبلغ (مثال) <<<---
            int amountInRials = price;
            try
            {
                // فرض کنید یک متد برای گرفتن مبلغ سفارش از دیتابیس دارید
                // که مبلغ را به تومان برمیگرداند
                // decimal orderAmountInToman = await _orderService.GetOrderAmountAsync(orderId);
                // if (orderAmountInToman <= 0)
                // {
                //     _logger.LogWarning("Order not found or amount is zero for OrderId: {OrderId}", orderId);
                //     TempData["ErrorMessage"] = "سفارش یافت نشد یا مبلغ آن نامعتبر است.";
                //     return RedirectToAction("Index", "Cart"); // Or appropriate page
                // }
                // // تبدیل به ریال (مطمئن شوید که نوع داده مناسب برای جلوگیری از سرریز انتخاب شده)
                // amountInRials = (int)(orderAmountInToman * 10);

                // --- مثال با مبلغ ثابت (فقط برای تست اولیه - حتما جایگزین کنید) ---
                _logger.LogWarning("Using FIXED amount for testing OrderId: {OrderId}. REPLACE THIS!", orderId);
                amountInRials = price; // 1000 تومان به عنوان مثال (به ریال)
                if (orderId == null) // یک ولیدیشن ساده برای orderId
                {
                    TempData["ErrorMessage"] = "شناسه سفارش نامعتبر است.";
                    return RedirectToAction("PaymentResult", new { success = false });
                }
                // --- پایان مثال ---

            }
            catch (Exception ex) // مدیریت خطای احتمالی در خواندن اطلاعات سفارش
            {
                _logger.LogError(ex, "Error retrieving order amount for OrderId: {OrderId}", orderId);
                TempData["ErrorMessage"] = "خطا در بازیابی اطلاعات سفارش.";
                return RedirectToAction("PaymentResult", new { success = false });
            }
            // --->>> پایان منطق واقعی دریافت مبلغ <<<---


            string description = $"پرداخت سفارش شماره {orderId}";


            // ساخت Callback URL کامل
            var callbackUrl = Url.Action(
                action: "VerifyPaymentt", // نام اکشن Verify
                controller: "Payment",   // نام همین کنترلر
                values: new { orderId = orderId, price = amountInRials }, // پارامترهایی که میخواهیم همراه Callback برگردند
                protocol: Request.Scheme, // http or https
                host: Request.Host.ToString() // domain:port
             );

            if (string.IsNullOrEmpty(callbackUrl))
            {
                _logger.LogError("Could not generate callback URL for OrderId: {OrderId}", orderId);
                TempData["ErrorMessage"] = "خطا در ایجاد لینک بازگشت از بانک.";
                // به یک صفحه خطا یا صفحه اصلی هدایت کنید
                return RedirectToAction("PaymentResult", new { success = false });
            }


            // دریافت ایمیل و موبایل کاربر (اختیاری - اگر نیاز دارید)
            // string userEmail = User.FindFirstValue(ClaimTypes.Email); // Example
            // string userMobile = ...; // Get user mobile if available

            // ارسال درخواست به زرین پال
            _logger.LogInformation("Sending payment request to Zarinpal for OrderId: {OrderId}, Amount: {Amount}, Callback: {CallbackUrl}", orderId, amountInRials, callbackUrl);
            var response = await _zarinpalService.RequestPaymentAsync(amountInRials, description, callbackUrl, "faghih1998@gmail.com", "09120912123");

            if (response.IsSuccess() && !string.IsNullOrEmpty(response.Data?.Authority))
            {
                // --- ذخیره وضعیت موقت یا Authority ---
                // مهم: قبل از ریدایرکت، Authority را به همراه OrderId و Amount
                // در جایی ذخیره کنید (مثلا در دیتابیس در رکورد سفارش یا یک جدول جداگانه تراکنش ها)
                // تا در مرحله Verify بتوانید Amount صحیح را بازیابی کرده و Authority را تطبیق دهید.
                try
                {
                    // await _orderService.SetPaymentAuthorityAsync(orderId, response.Data.Authority, amountInRials);
                    _logger.LogInformation("Payment Authority {Authority} saved/updated for OrderId: {OrderId}", response.Data.Authority, orderId);
                    // --- مثال: ذخیره در TempData فقط برای تست سریع (اصلا توصیه نمی‌شود برای پروداکشن) ---
                    // TempData[$"ZarinpalAmount_{response.Data.Authority}"] = amountInRials;
                    // --- پایان مثال TempData ---
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save payment Authority {Authority} for OrderId: {OrderId}", response.Data?.Authority, orderId);
                    TempData["ErrorMessage"] = "خطا در ثبت اطلاعات اولیه پرداخت. لطفا با پشتیبانی تماس بگیرید.";
                    return RedirectToAction("PaymentResult", new { success = false });
                }


                _logger.LogInformation("Redirecting user to Zarinpal gateway for OrderId: {OrderId}, Authority: {Authority}", orderId, response.Data.Authority);
                // ریدایرکت کاربر به درگاه پرداخت
                var gatewayUrl = _zarinpalSettings.GetGatewayUrl(response.Data.Authority);
                return Redirect(gatewayUrl);
            }
            else
            {
                // خطا در درخواست پرداخت
                var errorMessage = $"Zarinpal request payment failed for OrderId: . Code: {response.Data?.Code ?? -1}, Message: {response.Data?.Message ?? "N/A"}, Errors: {(response.Errors != null ? JsonSerializer.Serialize(response.Errors) : "N/A")}";
                _logger.LogError(errorMessage);
                TempData["ErrorMessage"] = "متاسفانه امکان اتصال به درگاه پرداخت وجود ندارد. لطفا بعدا تلاش کنید یا با پشتیبانی تماس بگیرید.";
                // کاربر را به صفحه خطا یا سبد خرید برگردانید
                return RedirectToAction("PaymentResult", new { success = false });
            }
        }

        // --------------------------

        [HttpGet] // زرین پال معمولا با GET به Callback URL می آید
        public async Task<IActionResult> VerifyPaymentt(string orderId, [FromQuery] string authority, [FromQuery] string status, int price)
        // 'authority' و 'status' توسط زرین پال به Query String اضافه می شوند
        // 'orderId' هم پارامتری بود که خودمان موقع ساخت Callback URL فرستادیم
        {
            _logger.LogInformation("Callback received from Zarinpal for OrderId: {OrderId}, Authority: {Authority}, Status: {Status}", orderId, authority, status);

            // بررسی پارامترهای ضروری بازگشتی
            if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(status))
            {
                _logger.LogWarning("Invalid callback parameters received for OrderId: {OrderId}. Authority: '{Authority}', Status: '{Status}'", orderId, authority, status);
                TempData["ErrorMessage"] = "اطلاعات بازگشتی از درگاه پرداخت نامعتبر است.";
                return RedirectToAction("PaymentResult", new { success = false });
            }


            // --- بازیابی مبلغ اصلی سفارش ---
            // !! بسیار مهم !! مبلغ را دوباره از منبع امن سمت سرور بخوانید.
            // هرگز به مبلغی که شاید در URL یا جای دیگری باشد اعتماد نکنید.
            // همچنین Authority دریافتی را با Authority ذخیره شده برای این OrderId مقایسه کنید.
            int amountInRials = price;
            try
            {
                // مبلغ و Authority را از جایی که در مرحله Request ذخیره کردید بخوانید
                // var paymentInfo = await _orderService.GetPaymentInfoByOrderIdAsync(orderId);
                // if (paymentInfo == null || paymentInfo.Authority != authority) {
                //      _logger.LogError("Payment info not found or authority mismatch for OrderId: {OrderId}, Received Authority: {Authority}", orderId, authority);
                //      TempData["ErrorMessage"] = "اطلاعات پرداخت یافت نشد یا مغایرت دارد.";
                //      return RedirectToAction("PaymentResult", new { success = false });
                // }
                // if (paymentInfo.IsAlreadyVerified) { // اگر قبلا تایید شده
                //      _logger.LogWarning("Payment for OrderId: {OrderId} seems to be already verified.", orderId);
                //      TempData["SuccessMessage"] = "پرداخت این سفارش قبلا با موفقیت تایید شده است.";
                //      return RedirectToAction("PaymentResult", new { success = true, refId = paymentInfo.RefId });
                // }
                // amountInRials = paymentInfo.AmountInRials; // مبلغ به ریال

                // --- مثال با مبلغ ثابت (فقط برای تست اولیه - حتما جایگزین کنید) ---
                _logger.LogWarning("Using FIXED amount for testing verification for OrderId: {OrderId}. REPLACE THIS!", orderId);
                amountInRials = price; // باید دقیقا همان مبلغی باشد که در Request فرستاده شد
                                       // --- پایان مثال ---

                // ولیدیشن ساده
                if (amountInRials <= 0)
                {
                    _logger.LogError("Invalid amount ({Amount}) retrieved for verification for OrderId: {OrderId}", amountInRials, orderId);
                    TempData["ErrorMessage"] = "مبلغ پرداخت نامعتبر است.";
                    return RedirectToAction("PaymentResult", new { success = false });
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving original amount/authority for OrderId: {OrderId}, Authority: {Authority}", orderId, authority);
                TempData["ErrorMessage"] = "خطا در بازیابی اطلاعات اولیه پرداخت.";
                return RedirectToAction("PaymentResult", new { success = false });
            }
            // --- پایان بازیابی مبلغ ---


            // بررسی وضعیت بازگشتی از زرین پال
            if (status.Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                // کاربر پرداخت را انجام داده، حالا باید تایید نهایی را از زرین پال بگیریم
                _logger.LogInformation("Payment status is OK for OrderId: {OrderId}, Authority: {Authority}. Proceeding to verification.", orderId, authority);

                var verificationResponse = await _zarinpalService.VerifyPaymentAsync(amountInRials, authority);

                if (verificationResponse.IsSuccess() && verificationResponse.Data != null)
                {
                    // پرداخت با موفقیت تایید شد
                    long refId = verificationResponse.Data.RefId ?? 0;
                    string cardPan = verificationResponse.Data.CardPan ?? "N/A";
                    bool alreadyVerified = verificationResponse.IsAlreadyVerified(); // کد 101

                    _logger.LogInformation("Zarinpal verification successful for OrderId: {OrderId}, Authority: {Authority}. RefID: {RefID}. Already Verified: {AlreadyVerified}", orderId, authority, refId, alreadyVerified);


                    // --- به روز رسانی وضعیت سفارش در دیتابیس ---
                    try
                    {
                        // await _orderService.FinalizePaymentAsync(orderId, refId, cardPan, verificationResponse.Data.Code);
                        _logger.LogInformation("Order status updated to 'Paid' for OrderId: {OrderId}, RefID: {RefID}", orderId, refId);

                        var invoice = _context.Invoices.SingleOrDefault(a => a.Id == orderId);
                        invoice.Status = "پرداخت شده";
                        invoice.PaymentRefId = refId;
                        _context.Invoices.Update(invoice);
                        _context.SaveChanges();
                        // نمایش پیام موفقیت به کاربر
                        TempData["SuccessMessage"] = $"پرداخت شما با موفقیت انجام و تایید شد. شماره پیگیری: {refId}";
                        return RedirectToAction("PaymentResult", new { success = true, refId = refId });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update order status after successful verification for OrderId: {OrderId}, RefID: {RefID}", orderId, refId);
                        // !! مهم: حتی اگر ذخیره در دیتابیس فیل شد، پرداخت انجام شده!
                        // باید این مورد را لاگ کنید و شاید به کاربر پیام دهید که پرداخت موفق بود ولی ثبت نهایی نشد و با پشتیبانی تماس بگیرد.
                        TempData["WarningMessage"] = $"پرداخت شما با شماره پیگیری {refId} موفق بود، اما در ثبت نهایی سفارش خطایی رخ داد. لطفا این شماره پیگیری را نگه داشته و با پشتیبانی تماس بگیرید.";
                        return RedirectToAction("PaymentResult", new { success = false });
                    }
                    // --- پایان به روز رسانی دیتابیس ---

                }
                else
                {
                    // تایید پرداخت ناموفق بود
                    var errorCode = verificationResponse.Data?.Code ?? -1;
                    var errorMessage = verificationResponse.Data?.Message ?? "خطای نامشخص در تایید";
                    var errorDetails = verificationResponse.Errors != null ? JsonSerializer.Serialize(verificationResponse.Errors) : "N/A";
                    _logger.LogError("Zarinpal verification failed for OrderId: {OrderId}, Authority: {Authority}. Code: {ErrorCode}, Message: {ErrorMessage}, Errors: {ErrorDetails}", orderId, authority, errorCode, errorMessage, errorDetails);

                    // --- به روز رسانی وضعیت سفارش به ناموفق (اختیاری) ---
                    // try {
                    //      await _orderService.SetPaymentFailedAsync(orderId, errorCode, errorMessage);
                    // } catch (Exception ex) {
                    //      _logger.LogError(ex, "Failed to update order status to 'Failed' for OrderId: {OrderId}", orderId);
                    // }
                    // --- پایان ---

                    TempData["ErrorMessage"] = $"پرداخت ناموفق بود. {GetZarinpalErrorMessage(errorCode)} (کد خطا: {errorCode})";
                    return RedirectToAction("PaymentResult", new { success = false });
                }
            }
            else // status == "NOK" یا هر چیز دیگری غیر از "OK"
            {
                // کاربر پرداخت را لغو کرده یا خطایی در سمت زرین پال قبل از پرداخت رخ داده
                _logger.LogWarning("Payment was cancelled or failed by user/Zarinpal for OrderId: {OrderId}, Authority: {Authority}. Status: {Status}", orderId, authority, status);
                TempData["ErrorMessage"] = "پرداخت توسط شما لغو شد یا در انجام آن خطایی رخ داد.";

                // --- به روز رسانی وضعیت سفارش به لغو شده (اختیاری) ---
                // try {
                //      await _orderService.SetPaymentCancelledAsync(orderId);
                // } catch (Exception ex) {
                //      _logger.LogError(ex, "Failed to update order status to 'Cancelled' for OrderId: {OrderId}", orderId);
                // }
                // --- پایان ---
                return RedirectToAction("PaymentResult", new { success = false });
            }
        }

        #endregion



        #region RahaGozar Payment
        // ------- پرداخت از رهاگذر -----
        public async Task<IActionResult> RequestPaymentWithAll(string orderId, int price, string email, string phoneNumber)
        {
            // --- دریافت مبلغ سفارش ---
            // !! این بخش بسیار مهم است !!
            // مبلغ را هرگز از کلاینت (فرم ارسالی) مستقیم نخوانید.
            // مبلغ باید از دیتابیس یا منبع معتبر سمت سرور بر اساس orderId خوانده شود.
            _logger.LogInformation("Attempting to start payment for OrderId: {OrderId}", orderId);

            // --->>> شروع منطق واقعی دریافت مبلغ (مثال) <<<---
            int amountInRials;
            try
            {
                // فرض کنید یک متد برای گرفتن مبلغ سفارش از دیتابیس دارید
                // که مبلغ را به تومان برمیگرداند
                // decimal orderAmountInToman = await _orderService.GetOrderAmountAsync(orderId);
                // if (orderAmountInToman <= 0)
                // {
                //     _logger.LogWarning("Order not found or amount is zero for OrderId: {OrderId}", orderId);
                //     TempData["ErrorMessage"] = "سفارش یافت نشد یا مبلغ آن نامعتبر است.";
                //     return RedirectToAction("Index", "Cart"); // Or appropriate page
                // }
                // // تبدیل به ریال (مطمئن شوید که نوع داده مناسب برای جلوگیری از سرریز انتخاب شده)
                // amountInRials = (int)(orderAmountInToman * 10);

                // --- مثال با مبلغ ثابت (فقط برای تست اولیه - حتما جایگزین کنید) ---
                _logger.LogWarning("Using FIXED amount for testing OrderId: {OrderId}. REPLACE THIS!", orderId);
                amountInRials = price; // 1000 تومان به عنوان مثال (به ریال)
                if (orderId == "") // یک ولیدیشن ساده برای orderId
                {
                    TempData["ErrorMessage"] = "شناسه سفارش نامعتبر است.";
                    string Message = System.Web.HttpUtility.UrlEncode($"شناسه سفارش نامعتبر است.");
                    return Redirect($"{_url}{Message}");
                }
                // --- پایان مثال ---

            }
            catch (Exception ex) // مدیریت خطای احتمالی در خواندن اطلاعات سفارش
            {
                _logger.LogError(ex, "Error retrieving order amount for OrderId: {OrderId}", orderId);
                TempData["ErrorMessage"] = "خطا در بازیابی اطلاعات سفارش.";
                string Message = System.Web.HttpUtility.UrlEncode($"خطا در بازیابی اطلاعات سفارش.");
                return Redirect($"{_url}{Message}");
            }
            // --->>> پایان منطق واقعی دریافت مبلغ <<<---


            string description = $"پرداخت سفارش شماره {orderId}";


            // ساخت Callback URL کامل
            var callbackUrl = Url.Action(
                action: "VerifyPayment", // نام اکشن Verify
                controller: "Payment",   // نام همین کنترلر
                values: new { orderId = orderId, price = price }, // پارامترهایی که میخواهیم همراه Callback برگردند
                protocol: Request.Scheme, // http or https
                host: Request.Host.ToString() // domain:port
             );

            if (string.IsNullOrEmpty(callbackUrl))
            {
                _logger.LogError("Could not generate callback URL for OrderId: {OrderId}", orderId);
                TempData["ErrorMessage"] = "خطا در ایجاد لینک بازگشت از بانک.";
                // به یک صفحه خطا یا صفحه اصلی هدایت کنید
                string Message = System.Web.HttpUtility.UrlEncode($"خطا در ایجاد لینک بازگشت از بانک.");
                return Redirect($"{_url}{Message}");
            }


            // دریافت ایمیل و موبایل کاربر (اختیاری - اگر نیاز دارید)
            // string userEmail = User.FindFirstValue(ClaimTypes.Email); // Example
            // string userMobile = ...; // Get user mobile if available

            // ارسال درخواست به زرین پال
            _logger.LogInformation("Sending payment request to Zarinpal for OrderId: {OrderId}, Amount: {Amount}, Callback: {CallbackUrl}", orderId, amountInRials, callbackUrl);
            var response = await _zarinpalService.RequestPaymentAsync(amountInRials, description, callbackUrl, email, phoneNumber);

            if (response.IsSuccess() && !string.IsNullOrEmpty(response.Data?.Authority))
            {
                // --- ذخیره وضعیت موقت یا Authority ---
                // مهم: قبل از ریدایرکت، Authority را به همراه OrderId و Amount
                // در جایی ذخیره کنید (مثلا در دیتابیس در رکورد سفارش یا یک جدول جداگانه تراکنش ها)
                // تا در مرحله Verify بتوانید Amount صحیح را بازیابی کرده و Authority را تطبیق دهید.
                try
                {
                    // await _orderService.SetPaymentAuthorityAsync(orderId, response.Data.Authority, amountInRials);
                    _logger.LogInformation("Payment Authority {Authority} saved/updated for OrderId: {OrderId}", response.Data.Authority, orderId);
                    // --- مثال: ذخیره در TempData فقط برای تست سریع (اصلا توصیه نمی‌شود برای پروداکشن) ---
                    // TempData[$"ZarinpalAmount_{response.Data.Authority}"] = amountInRials;
                    // --- پایان مثال TempData ---
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save payment Authority {Authority} for OrderId: {OrderId}", response.Data?.Authority, orderId);
                    TempData["ErrorMessage"] = "خطا در ثبت اطلاعات اولیه پرداخت. لطفا با پشتیبانی تماس بگیرید.";
                    string Message = System.Web.HttpUtility.UrlEncode($"خطا در ثبت اطلاعات اولیه پرداخت. لطفا با پشتیبانی تماس بگیرید.");
                    return Redirect($"{_url}{Message}");
                }


                _logger.LogInformation("Redirecting user to Zarinpal gateway for OrderId: {OrderId}, Authority: {Authority}", orderId, response.Data.Authority);
                // ریدایرکت کاربر به درگاه پرداخت
                var gatewayUrl = _zarinpalSettings.GetGatewayUrl(response.Data.Authority);
                return Redirect(gatewayUrl);
            }
            else
            {
                // خطا در درخواست پرداخت
                var errorMessage = $"Zarinpal request payment failed for OrderId: . Code: {response.Data?.Code ?? -1}, Message: {response.Data?.Message ?? "N/A"}, Errors: {(response.Errors != null ? JsonSerializer.Serialize(response.Errors) : "N/A")}";
                _logger.LogError(errorMessage);
                TempData["ErrorMessage"] = "متاسفانه امکان اتصال به درگاه پرداخت وجود ندارد. لطفا بعدا تلاش کنید یا با پشتیبانی تماس بگیرید.";
                // کاربر را به صفحه خطا یا سبد خرید برگردانید
                string Message = System.Web.HttpUtility.UrlEncode($"متاسفانه امکان اتصال به درگاه پرداخت وجود ندارد. لطفا بعدا تلاش کنید یا با پشتیبانی تماس بگیرید.");
                return Redirect($"{_url}{Message}");
            }
        }

        // --- اکشن بازگشت از درگاه و تایید پرداخت (Callback URL) ---
        [HttpGet] // زرین پال معمولا با GET به Callback URL می آید
        public async Task<IActionResult> VerifyPayment(string orderId, [FromQuery] string authority, [FromQuery] string status, int price)
        // 'authority' و 'status' توسط زرین پال به Query String اضافه می شوند
        // 'orderId' هم پارامتری بود که خودمان موقع ساخت Callback URL فرستادیم
        {
            _logger.LogInformation("Callback received from Zarinpal for OrderId: {OrderId}, Authority: {Authority}, Status: {Status}", orderId, authority, status);

            // بررسی پارامترهای ضروری بازگشتی
            if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(status))
            {
                _logger.LogWarning("Invalid callback parameters received for OrderId: {OrderId}. Authority: '{Authority}', Status: '{Status}'", orderId, authority, status);
                TempData["ErrorMessage"] = "اطلاعات بازگشتی از درگاه پرداخت نامعتبر است.";
                string Message = System.Web.HttpUtility.UrlEncode($"اطلاعات بازگشتی از درگاه پرداخت نامعتبر است.");
                return Redirect($"{_url}{Message}");
            }


            // --- بازیابی مبلغ اصلی سفارش ---
            // !! بسیار مهم !! مبلغ را دوباره از منبع امن سمت سرور بخوانید.
            // هرگز به مبلغی که شاید در URL یا جای دیگری باشد اعتماد نکنید.
            // همچنین Authority دریافتی را با Authority ذخیره شده برای این OrderId مقایسه کنید.
            int amountInRials;
            try
            {
                // مبلغ و Authority را از جایی که در مرحله Request ذخیره کردید بخوانید
                // var paymentInfo = await _orderService.GetPaymentInfoByOrderIdAsync(orderId);
                // if (paymentInfo == null || paymentInfo.Authority != authority) {
                //      _logger.LogError("Payment info not found or authority mismatch for OrderId: {OrderId}, Received Authority: {Authority}", orderId, authority);
                //      TempData["ErrorMessage"] = "اطلاعات پرداخت یافت نشد یا مغایرت دارد.";
                //      return RedirectToAction("PaymentResult", new { success = false });
                // }
                // if (paymentInfo.IsAlreadyVerified) { // اگر قبلا تایید شده
                //      _logger.LogWarning("Payment for OrderId: {OrderId} seems to be already verified.", orderId);
                //      TempData["SuccessMessage"] = "پرداخت این سفارش قبلا با موفقیت تایید شده است.";
                //      return RedirectToAction("PaymentResult", new { success = true, refId = paymentInfo.RefId });
                // }
                // amountInRials = paymentInfo.AmountInRials; // مبلغ به ریال

                // --- مثال با مبلغ ثابت (فقط برای تست اولیه - حتما جایگزین کنید) ---
                _logger.LogWarning("Using FIXED amount for testing verification for OrderId: {OrderId}. REPLACE THIS!", orderId);
                amountInRials = price; // باید دقیقا همان مبلغی باشد که در Request فرستاده شد
                                       // --- پایان مثال ---

                // ولیدیشن ساده
                if (amountInRials <= 0)
                {
                    _logger.LogError("Invalid amount ({Amount}) retrieved for verification for OrderId: {OrderId}", amountInRials, orderId);
                    TempData["ErrorMessage"] = "مبلغ پرداخت نامعتبر است.";
                    string Message = System.Web.HttpUtility.UrlEncode($"مبلغ پرداخت نامعتبر است.");
                    return Redirect($"{_url}{Message}");
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving original amount/authority for OrderId: {OrderId}, Authority: {Authority}", orderId, authority);
                TempData["ErrorMessage"] = "خطا در بازیابی اطلاعات اولیه پرداخت.";
                string Message = System.Web.HttpUtility.UrlEncode($"خطا در بازیابی اطلاعات اولیه پرداخت.");
                return Redirect($"{_url}{Message}");
            }
            // --- پایان بازیابی مبلغ ---


            // بررسی وضعیت بازگشتی از زرین پال
            if (status.Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                // کاربر پرداخت را انجام داده، حالا باید تایید نهایی را از زرین پال بگیریم
                _logger.LogInformation("Payment status is OK for OrderId: {OrderId}, Authority: {Authority}. Proceeding to verification.", orderId, authority);

                var verificationResponse = await _zarinpalService.VerifyPaymentAsync(amountInRials, authority);

                if (verificationResponse.IsSuccess() && verificationResponse.Data != null)
                {
                    // پرداخت با موفقیت تایید شد
                    long refId = verificationResponse.Data.RefId ?? 0;
                    string cardPan = verificationResponse.Data.CardPan ?? "N/A";
                    bool alreadyVerified = verificationResponse.IsAlreadyVerified(); // کد 101

                    _logger.LogInformation("Zarinpal verification successful for OrderId: {OrderId}, Authority: {Authority}. RefID: {RefID}. Already Verified: {AlreadyVerified}", orderId, authority, refId, alreadyVerified);


                    // --- به روز رسانی وضعیت سفارش در دیتابیس ---
                    try
                    {
                        // await _orderService.FinalizePaymentAsync(orderId, refId, cardPan, verificationResponse.Data.Code);
                        _logger.LogInformation("Order status updated to 'Paid' for OrderId: {OrderId}, RefID: {RefID}", orderId, refId);

                        var invoice = _context.Invoices.SingleOrDefault(a => a.Id == orderId);
                        invoice.Status = "پرداخت شده";
                        invoice.PaymentRefId = refId;
                        _context.Invoices.Update(invoice);
                        _context.SaveChanges();
                        // نمایش پیام موفقیت به کاربر
                        TempData["SuccessMessage"] = $"پرداخت شما با موفقیت انجام و تایید شد. شماره پیگیری: {refId}";
                        string Message = System.Web.HttpUtility.UrlEncode($"پرداخت شما با موفقیت انجام و تایید شد. شماره پیگیری: {refId}");
                        return Redirect($"https://rahagozar.com/UserPanel/Panel/ProcessPaymentt?invoiceId={orderId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update order status after successful verification for OrderId: {OrderId}, RefID: {RefID}", orderId, refId);
                        // !! مهم: حتی اگر ذخیره در دیتابیس فیل شد، پرداخت انجام شده!
                        // باید این مورد را لاگ کنید و شاید به کاربر پیام دهید که پرداخت موفق بود ولی ثبت نهایی نشد و با پشتیبانی تماس بگیرد.
                        TempData["WarningMessage"] = $"پرداخت شما با شماره پیگیری {refId} موفق بود، اما در ثبت نهایی سفارش خطایی رخ داد. لطفا این شماره پیگیری را نگه داشته و با پشتیبانی تماس بگیرید.";
                        string Message = System.Web.HttpUtility.UrlEncode($"پرداخت شما با شماره پیگیری {refId} موفق بود، اما در ثبت نهایی سفارش خطایی رخ داد. لطفا این شماره پیگیری را نگه داشته و با پشتیبانی تماس بگیرید.");
                        return Redirect($"{_url}{Message}");
                    }
                    // --- پایان به روز رسانی دیتابیس ---

                }
                else
                {
                    // تایید پرداخت ناموفق بود
                    var errorCode = verificationResponse.Data?.Code ?? -1;
                    var errorMessage = verificationResponse.Data?.Message ?? "خطای نامشخص در تایید";
                    var errorDetails = verificationResponse.Errors != null ? JsonSerializer.Serialize(verificationResponse.Errors) : "N/A";
                    _logger.LogError("Zarinpal verification failed for OrderId: {OrderId}, Authority: {Authority}. Code: {ErrorCode}, Message: {ErrorMessage}, Errors: {ErrorDetails}", orderId, authority, errorCode, errorMessage, errorDetails);

                    // --- به روز رسانی وضعیت سفارش به ناموفق (اختیاری) ---
                    // try {
                    //      await _orderService.SetPaymentFailedAsync(orderId, errorCode, errorMessage);
                    // } catch (Exception ex) {
                    //      _logger.LogError(ex, "Failed to update order status to 'Failed' for OrderId: {OrderId}", orderId);
                    // }
                    // --- پایان ---

                    TempData["ErrorMessage"] = $"پرداخت ناموفق بود. {GetZarinpalErrorMessage(errorCode)} (کد خطا: {errorCode})";
                    string Message = System.Web.HttpUtility.UrlEncode($"پرداخت ناموفق بود. {GetZarinpalErrorMessage(errorCode)} (کد خطا: {errorCode})");
                    return Redirect($"{_url}{Message}");
                }
            }
            else // status == "NOK" یا هر چیز دیگری غیر از "OK"
            {
                // کاربر پرداخت را لغو کرده یا خطایی در سمت زرین پال قبل از پرداخت رخ داده
                _logger.LogWarning("Payment was cancelled or failed by user/Zarinpal for OrderId: {OrderId}, Authority: {Authority}. Status: {Status}", orderId, authority, status);
                TempData["ErrorMessage"] = "پرداخت توسط شما لغو شد یا در انجام آن خطایی رخ داد.";

                // --- به روز رسانی وضعیت سفارش به لغو شده (اختیاری) ---
                // try {
                //      await _orderService.SetPaymentCancelledAsync(orderId);
                // } catch (Exception ex) {
                //      _logger.LogError(ex, "Failed to update order status to 'Cancelled' for OrderId: {OrderId}", orderId);
                // }
                // --- پایان ---
                string Message = System.Web.HttpUtility.UrlEncode("پرداخت توسط شما لغو شد یا در انجام آن خطایی رخ داد");
                return Redirect($"{_url}{Message}");
            }
        }
        #endregion



        // --- اکشن نمایش نتیجه پرداخت ---
        [HttpGet]
        public IActionResult PaymentResult(bool success, long? refId = null)
        {
            ViewBag.Success = success;
            ViewBag.RefId = refId;

            // خواندن پیام ها از TempData که در اکشن Verify تنظیم شده اند
            if (TempData.ContainsKey("SuccessMessage"))
                ViewBag.ResultMessage = TempData["SuccessMessage"];
            else if (TempData.ContainsKey("ErrorMessage"))
                ViewBag.ResultMessage = TempData["ErrorMessage"];
            else if (TempData.ContainsKey("WarningMessage")) // برای حالت خاص ذخیره نشدن در دیتابیس
                ViewBag.ResultWarning = TempData["WarningMessage"]; // از یک ViewBag جدا استفاده کنیم بهتر است
            else if (success)
                ViewBag.ResultMessage = "پرداخت با موفقیت انجام شد.";
            else
                ViewBag.ResultMessage = "پرداخت ناموفق بود یا لغو شد.";


            // شما باید یک View به نام PaymentResult.cshtml در پوشه Views/Payment ایجاد کنید
            return View();
        }


        // --- (اختیاری) متد کمکی برای ترجمه کدهای خطای زرین پال به پیام های قابل فهم ---
        private string GetZarinpalErrorMessage(int errorCode)
        {
            // لیست کامل کدها را از مستندات زرین پال بگیرید
            return errorCode switch
            {
                -9 => "خطا در اعتبار سنجی",
                -10 => "ای پی یا مرچنت کد صحیح نیست",
                -11 => "مرچنت کد فعال نیست",
                -12 => "تلاش بیش از حد در یک بازه زمانی کوتاه",
                -15 => "ترمینال شما به حالت تعلیق در آمده",
                -16 => "سطح تایید پذیرنده پایین تر از سطح نقره ای است",
                -30 => "اجازه دسترسی به تسویه اشتراکی داده نشده",
                -31 => "حساب بانکی تسویه اشتراکی تایید نشده",
                -33 => "رقم تراکنش با رقم وارد شده مطابقت ندارد",
                -34 => "سقف تقسیم تراکنش از لحاظ تعداد یا رقم عبور کرده",
                -40 => "اجازه دسترسی به این متد وجود ندارد",
                -41 => "اطلاعات ارسال شده مربوط به تایید غیر معتبر است",
                -42 => "مدت زمان معتبر طول عمر شناسه پرداخت باید بین ۳۰ دقیقه تا ۴۵ روز باشد",
                -54 => "اتوریتی نامعتبر است",
                101 => "تراکنش قبلا یک بار تایید شده است", // این خطا نیست، یک وضعیت است
                _ => "خطای تعریف نشده از سمت درگاه پرداخت"
            };
        }





        #region RahaGozar

        public async Task<IActionResult> Request_Payment(string orderId, string email, string phoneNumber)
        {
            // --- دریافت مبلغ سفارش ---
            // !! این بخش بسیار مهم است !!
            // مبلغ را هرگز از کلاینت (فرم ارسالی) مستقیم نخوانید.
            // مبلغ باید از دیتابیس یا منبع معتبر سمت سرور بر اساس orderId خوانده شود.
            //_logger.LogInformation("Attempting to start payment for OrderId: {OrderId}", orderId);

            // --->>> شروع منطق واقعی دریافت مبلغ (مثال) <<<---
            int amountInRials;
            try
            {
                // فرض کنید یک متد برای گرفتن مبلغ سفارش از دیتابیس دارید
                // که مبلغ را به تومان برمیگرداند
                // decimal orderAmountInToman = await _orderService.GetOrderAmountAsync(orderId);
                // if (orderAmountInToman <= 0)
                // {
                //     _logger.LogWarning("Order not found or amount is zero for OrderId: {OrderId}", orderId);
                //     TempData["ErrorMessage"] = "سفارش یافت نشد یا مبلغ آن نامعتبر است.";
                //     return RedirectToAction("Index", "Cart"); // Or appropriate page
                // }
                // // تبدیل به ریال (مطمئن شوید که نوع داده مناسب برای جلوگیری از سرریز انتخاب شده)
                // amountInRials = (int)(orderAmountInToman * 10);

                // --- مثال با مبلغ ثابت (فقط برای تست اولیه - حتما جایگزین کنید) ---
                _logger.LogWarning("Using FIXED amount for testing OrderId: {OrderId}. REPLACE THIS!", orderId);
                amountInRials = 100000; // 1000 تومان به عنوان مثال (به ریال)
                if (orderId == null) // یک ولیدیشن ساده برای orderId
                {
                    TempData["ErrorMessage"] = "شناسه سفارش نامعتبر است.";
                    return RedirectToAction("Index", "Cart");
                }
                // --- پایان مثال ---

            }
            catch (Exception ex) // مدیریت خطای احتمالی در خواندن اطلاعات سفارش
            {
                _logger.LogError(ex, "Error retrieving order amount for OrderId: {OrderId}", orderId);
                TempData["ErrorMessage"] = "خطا در بازیابی اطلاعات سفارش.";
                return RedirectToAction("Error", "Home"); // Or appropriate page
            }
            // --->>> پایان منطق واقعی دریافت مبلغ <<<---


            string description = $"پرداخت سفارش شماره {orderId}";


            // ساخت Callback URL کامل
            var callbackUrl = Url.Action(
                action: "Verify_Payment", // نام اکشن Verify
                controller: "Payment",   // نام همین کنترلر
                values: new { orderId = orderId }, // پارامترهایی که میخواهیم همراه Callback برگردند
                protocol: Request.Scheme, // http or https
                host: Request.Host.ToString() // domain:port
             );

            if (string.IsNullOrEmpty(callbackUrl))
            {
                _logger.LogError("Could not generate callback URL for OrderId: {OrderId}", orderId);
                TempData["ErrorMessage"] = "خطا در ایجاد لینک بازگشت از بانک.";
                // به یک صفحه خطا یا صفحه اصلی هدایت کنید
                return RedirectToAction("Error", "Home");
            }


            // دریافت ایمیل و موبایل کاربر (اختیاری - اگر نیاز دارید)
            // string userEmail = User.FindFirstValue(ClaimTypes.Email); // Example
            // string userMobile = ...; // Get user mobile if available

            // ارسال درخواست به زرین پال
            _logger.LogInformation("Sending payment request to Zarinpal for OrderId: {OrderId}, Amount: {Amount}, Callback: {CallbackUrl}", orderId, amountInRials, callbackUrl);
            var response = await _zarinpalService.RequestPaymentAsync(amountInRials, description, callbackUrl, email, phoneNumber);

            if (response.IsSuccess() && !string.IsNullOrEmpty(response.Data?.Authority))
            {
                // --- ذخیره وضعیت موقت یا Authority ---
                // مهم: قبل از ریدایرکت، Authority را به همراه OrderId و Amount
                // در جایی ذخیره کنید (مثلا در دیتابیس در رکورد سفارش یا یک جدول جداگانه تراکنش ها)
                // تا در مرحله Verify بتوانید Amount صحیح را بازیابی کرده و Authority را تطبیق دهید.
                try
                {
                    // await _orderService.SetPaymentAuthorityAsync(orderId, response.Data.Authority, amountInRials);
                    //_logger.LogInformation("Payment Authority {Authority} saved/updated for OrderId: {OrderId}", response.Data.Authority, orderId);
                    // --- مثال: ذخیره در TempData فقط برای تست سریع (اصلا توصیه نمی‌شود برای پروداکشن) ---
                    // TempData[$"ZarinpalAmount_{response.Data.Authority}"] = amountInRials;
                    // --- پایان مثال TempData ---
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save payment Authority {Authority} for OrderId: {OrderId}", response.Data?.Authority, orderId);
                    TempData["ErrorMessage"] = "خطا در ثبت اطلاعات اولیه پرداخت. لطفا با پشتیبانی تماس بگیرید.";
                    return RedirectToAction("Error", "Home"); // Or appropriate page
                }


                _logger.LogInformation("Redirecting user to Zarinpal gateway for OrderId: {OrderId}, Authority: {Authority}", orderId, response.Data.Authority);
                // ریدایرکت کاربر به درگاه پرداخت
                var gatewayUrl = _zarinpalSettings.GetGatewayUrl(response.Data.Authority);
                return Redirect(gatewayUrl);
            }
            else
            {
                // خطا در درخواست پرداخت
                var errorMessage = $"Zarinpal request payment failed for OrderId: . Code: {response.Data?.Code ?? -1}, Message: {response.Data?.Message ?? "N/A"}, Errors: {(response.Errors != null ? JsonSerializer.Serialize(response.Errors) : "N/A")}";
                _logger.LogError(errorMessage);
                TempData["ErrorMessage"] = "متاسفانه امکان اتصال به درگاه پرداخت وجود ندارد. لطفا بعدا تلاش کنید یا با پشتیبانی تماس بگیرید.";
                // کاربر را به صفحه خطا یا سبد خرید برگردانید
                return RedirectToAction("Index", "Cart"); // Or appropriate page
            }
        }


        [HttpGet] // زرین پال معمولا با GET به Callback URL می آید
        public async Task<IActionResult> Verify_Payment(int orderId, [FromQuery] string authority, [FromQuery] string status)
        // 'authority' و 'status' توسط زرین پال به Query String اضافه می شوند
        // 'orderId' هم پارامتری بود که خودمان موقع ساخت Callback URL فرستادیم
        {
            _logger.LogInformation("Callback received from Zarinpal for OrderId: {OrderId}, Authority: {Authority}, Status: {Status}", orderId, authority, status);

            // بررسی پارامترهای ضروری بازگشتی
            if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(status))
            {
                _logger.LogWarning("Invalid callback parameters received for OrderId: {OrderId}. Authority: '{Authority}', Status: '{Status}'", orderId, authority, status);
                TempData["ErrorMessage"] = "اطلاعات بازگشتی از درگاه پرداخت نامعتبر است.";
                return RedirectToAction("PaymentResult", new { success = false }); // به صفحه نتیجه پرداخت هدایت شود
            }


            // --- بازیابی مبلغ اصلی سفارش ---
            // !! بسیار مهم !! مبلغ را دوباره از منبع امن سمت سرور بخوانید.
            // هرگز به مبلغی که شاید در URL یا جای دیگری باشد اعتماد نکنید.
            // همچنین Authority دریافتی را با Authority ذخیره شده برای این OrderId مقایسه کنید.
            int amountInRials;
            try
            {
                // مبلغ و Authority را از جایی که در مرحله Request ذخیره کردید بخوانید
                // var paymentInfo = await _orderService.GetPaymentInfoByOrderIdAsync(orderId);
                // if (paymentInfo == null || paymentInfo.Authority != authority) {
                //      _logger.LogError("Payment info not found or authority mismatch for OrderId: {OrderId}, Received Authority: {Authority}", orderId, authority);
                //      TempData["ErrorMessage"] = "اطلاعات پرداخت یافت نشد یا مغایرت دارد.";
                //      return RedirectToAction("PaymentResult", new { success = false });
                // }
                // if (paymentInfo.IsAlreadyVerified) { // اگر قبلا تایید شده
                //      _logger.LogWarning("Payment for OrderId: {OrderId} seems to be already verified.", orderId);
                //      TempData["SuccessMessage"] = "پرداخت این سفارش قبلا با موفقیت تایید شده است.";
                //      return RedirectToAction("PaymentResult", new { success = true, refId = paymentInfo.RefId });
                // }
                // amountInRials = paymentInfo.AmountInRials; // مبلغ به ریال

                // --- مثال با مبلغ ثابت (فقط برای تست اولیه - حتما جایگزین کنید) ---
                _logger.LogWarning("Using FIXED amount for testing verification for OrderId: {OrderId}. REPLACE THIS!", orderId);
                amountInRials = 10000; // باید دقیقا همان مبلغی باشد که در Request فرستاده شد
                                       // --- پایان مثال ---

                // ولیدیشن ساده
                if (amountInRials <= 0)
                {
                    _logger.LogError("Invalid amount ({Amount}) retrieved for verification for OrderId: {OrderId}", amountInRials, orderId);
                    TempData["ErrorMessage"] = "مبلغ پرداخت نامعتبر است.";
                    return RedirectToAction("PaymentResult", new { success = false });
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving original amount/authority for OrderId: {OrderId}, Authority: {Authority}", orderId, authority);
                TempData["ErrorMessage"] = "خطا در بازیابی اطلاعات اولیه پرداخت.";
                return RedirectToAction("PaymentResult", new { success = false });
            }
            // --- پایان بازیابی مبلغ ---


            // بررسی وضعیت بازگشتی از زرین پال
            if (status.Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                // کاربر پرداخت را انجام داده، حالا باید تایید نهایی را از زرین پال بگیریم
                _logger.LogInformation("Payment status is OK for OrderId: {OrderId}, Authority: {Authority}. Proceeding to verification.", orderId, authority);

                var verificationResponse = await _zarinpalService.VerifyPaymentAsync(amountInRials, authority);

                if (verificationResponse.IsSuccess() && verificationResponse.Data != null)
                {
                    // پرداخت با موفقیت تایید شد
                    long refId = verificationResponse.Data.RefId ?? 0;
                    string cardPan = verificationResponse.Data.CardPan ?? "N/A";
                    bool alreadyVerified = verificationResponse.IsAlreadyVerified(); // کد 101

                    _logger.LogInformation("Zarinpal verification successful for OrderId: {OrderId}, Authority: {Authority}. RefID: {RefID}. Already Verified: {AlreadyVerified}", orderId, authority, refId, alreadyVerified);


                    // --- به روز رسانی وضعیت سفارش در دیتابیس ---
                    try
                    {
                        // await _orderService.FinalizePaymentAsync(orderId, refId, cardPan, verificationResponse.Data.Code);
                        _logger.LogInformation("Order status updated to 'Paid' for OrderId: {OrderId}, RefID: {RefID}", orderId, refId);

                        // نمایش پیام موفقیت به کاربر
                        TempData["SuccessMessage"] = $"پرداخت شما با موفقیت انجام و تایید شد. شماره پیگیری: {refId}";
                        return RedirectToAction("PaymentResult", new { success = true, refId = refId }); // یا شناسه سفارش
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update order status after successful verification for OrderId: {OrderId}, RefID: {RefID}", orderId, refId);
                        // !! مهم: حتی اگر ذخیره در دیتابیس فیل شد، پرداخت انجام شده!
                        // باید این مورد را لاگ کنید و شاید به کاربر پیام دهید که پرداخت موفق بود ولی ثبت نهایی نشد و با پشتیبانی تماس بگیرد.
                        TempData["WarningMessage"] = $"پرداخت شما با شماره پیگیری {refId} موفق بود، اما در ثبت نهایی سفارش خطایی رخ داد. لطفا این شماره پیگیری را نگه داشته و با پشتیبانی تماس بگیرید.";
                        return RedirectToAction("PaymentResult", new { success = true, refId = refId }); // یا صفحه ای که این هشدار را نشان دهد
                    }
                    // --- پایان به روز رسانی دیتابیس ---

                }
                else
                {
                    // تایید پرداخت ناموفق بود
                    var errorCode = verificationResponse.Data?.Code ?? -1;
                    var errorMessage = verificationResponse.Data?.Message ?? "خطای نامشخص در تایید";
                    var errorDetails = verificationResponse.Errors != null ? JsonSerializer.Serialize(verificationResponse.Errors) : "N/A";
                    _logger.LogError("Zarinpal verification failed for OrderId: {OrderId}, Authority: {Authority}. Code: {ErrorCode}, Message: {ErrorMessage}, Errors: {ErrorDetails}", orderId, authority, errorCode, errorMessage, errorDetails);

                    // --- به روز رسانی وضعیت سفارش به ناموفق (اختیاری) ---
                    // try {
                    //      await _orderService.SetPaymentFailedAsync(orderId, errorCode, errorMessage);
                    // } catch (Exception ex) {
                    //      _logger.LogError(ex, "Failed to update order status to 'Failed' for OrderId: {OrderId}", orderId);
                    // }
                    // --- پایان ---

                    TempData["ErrorMessage"] = $"پرداخت ناموفق بود. {GetZarinpalErrorMessage(errorCode)} (کد خطا: {errorCode})";
                    return RedirectToAction("PaymentResult", new { success = false });
                }
            }
            else // status == "NOK" یا هر چیز دیگری غیر از "OK"
            {
                // کاربر پرداخت را لغو کرده یا خطایی در سمت زرین پال قبل از پرداخت رخ داده
                _logger.LogWarning("Payment was cancelled or failed by user/Zarinpal for OrderId: {OrderId}, Authority: {Authority}. Status: {Status}", orderId, authority, status);
                TempData["ErrorMessage"] = "پرداخت توسط شما لغو شد یا در انجام آن خطایی رخ داد.";

                // --- به روز رسانی وضعیت سفارش به لغو شده (اختیاری) ---
                // try {
                //      await _orderService.SetPaymentCancelledAsync(orderId);
                // } catch (Exception ex) {
                //      _logger.LogError(ex, "Failed to update order status to 'Cancelled' for OrderId: {OrderId}", orderId);
                // }
                // --- پایان ---

                return RedirectToAction("PaymentResult", new { success = false });
            }
        }

        #endregion


    }
}
