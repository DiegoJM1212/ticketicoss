using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ticketicos.Pages.administrativo
{
    public class AdminSoporteModel : PageModel
    {
        public IActionResult OnGet()
        {
            string? userType = HttpContext.Session.GetString("UserType");
            if (userType != "admin")
            {
                return RedirectToPage("/Cliente/Login");
            }

            return Page();
        }
    }
}