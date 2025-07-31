using Microsoft.AspNetCore.Mvc;

public class CalendarController : Controller
{
    public IActionResult Index(string resourceType)
    {
        ViewBag.ResourceType = resourceType;
        ViewBag.Title = resourceType switch
        {
            "library" => "Kütüphane Rezervasyonları",
            "room" => "Çalışma Odaları Rezervasyonları",
            "conference" => "Konferans Salonu Rezervasyonları",
            _ => "Rezervasyonlar"
        };
        return View();
    }
}