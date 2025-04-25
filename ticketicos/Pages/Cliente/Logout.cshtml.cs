using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ticketicos.Pages.Cliente
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnGet()
        {
            // Limpiar la sesión del usuario
            HttpContext.Session.Clear();

            // Redirigir al login
            return RedirectToPage("/Cliente/Login");
        }
    }
}
