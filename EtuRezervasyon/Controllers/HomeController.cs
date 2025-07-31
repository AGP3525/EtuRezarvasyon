using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EtuRezervasyon.Models;

namespace EtuRezervasyon.Controllers;

public class HomeController : Controller
{

    public IActionResult Index()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            if (User.IsInRole("Admin")) {
                return RedirectToAction("Index", "SuperAdmin");
            }
        }

        return View();
    }

}