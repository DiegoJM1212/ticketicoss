using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Linq;
using ticketicos.Data;
using ticketicos.Models;
using Microsoft.AspNetCore.Http;

namespace ticketicos.Pages
{
    public class PerfilModel : PageModel
    {
        private readonly VerEventoContext _context;

        public PerfilModel(VerEventoContext context)
        {
            _context = context;
        }

        public string NombreUsuario { get; set; }

        public void OnGet()
        {
            var correoUsuarioActual = User.Identity.Name;

            NombreUsuario = _context.Usuarios
                .Where(u => u.Correo == correoUsuarioActual)
                .Select(u => u.Nombre)
                .FirstOrDefault() ?? "Usuario";
        }

    }
}
