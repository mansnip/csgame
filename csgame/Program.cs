using csgame.Context;
using csgame.Models;
using csgame.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// --- ثبت تنظیمات زرین پال ---
// خواندن بخش ZarinpalSettings از appsettings.json و اتصال آن به کلاس ZarinpalSettings
builder.Services.Configure<ZarinpalSettings>(builder.Configuration.GetSection("ZarinpalSettings"));

// اضافه کردن DbContext
builder.Services.AddDbContext<csGameDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


// --- ثبت HttpClient برای زرین پال ---
// استفاده از IHttpClientFactory توصیه می شود
builder.Services.AddHttpClient("ZarinpalClient", client =>
{
    // می توانید تنظیمات پیش فرض برای هدرها یا BaseAddress اینجا اضافه کنید اگر نیاز بود
    // client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// --- ثبت سرویس زرین پال ---
// ثبت به صورت Scoped یا Transient معمولا مناسب است
builder.Services.AddScoped<IZarinpalService, ZarinpalService>();

// --- !! ثبت سرویس سفارشات شما !! ---
// شما باید سرویس مربوط به مدیریت سفارشات خودتان را هم اینجا ثبت کنید
// builder.Services.AddScoped<IOrderService, OrderService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    // در حالت توسعه، مقدار UseSandbox را از تنظیمات بخوانید و لاگ کنید
    var zarinpalSettings = app.Services.GetRequiredService<IOptions<ZarinpalSettings>>().Value;
    Console.WriteLine($"Zarinpal Sandbox Mode: {zarinpalSettings.UseSandbox}");
    Console.WriteLine($"Zarinpal Merchant ID: {zarinpalSettings.MerchantId}");
    Console.WriteLine($"Zarinpal Callback Base URL: {zarinpalSettings.CallbackUrlBase}"); // Callback base را لاگ کنید
}

    app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
