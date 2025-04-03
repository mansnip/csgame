using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace csgame.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Pay(int gold)
    {
        if (gold == 0)
        {
            return View("Index");
        }
        int price = gold * 15000;
        string email = "fasterman31@gmail.com";
        string phoneNumber = "09214232622";
        string orderId = "111";

        return Redirect($"/Payment/RequestPaymentWithAll?orderId={orderId}&price={price}&email={email}&phoneNumber={phoneNumber}");
    }
}
