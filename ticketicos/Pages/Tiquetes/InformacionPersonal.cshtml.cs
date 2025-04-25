using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ticketicos.Data;
using ticketicos.Models;

namespace ticketicos.Pages.Tiquetes
{
    public class InformacionPersonalModel : PageModel
    {
        private readonly VerEventoContext _context;

        public InformacionPersonalModel(VerEventoContext context)
        {
            _context = context;
        }

        [BindProperty]
        public UsuarioViewModel Usuario { get; set; } = new UsuarioViewModel();

        [BindProperty]
        public CambioPasswordViewModel CambioPassword { get; set; } = new CambioPasswordViewModel();

        public bool UsuarioAutenticado { get; set; }
        public string? MensajeExito { get; set; }
        public string? MensajeError { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Verificar si el usuario está autenticado
            var userId = HttpContext.Session.GetInt32("UserId");
            UsuarioAutenticado = userId.HasValue && userId.Value > 0;

            if (!UsuarioAutenticado)
            {
                return Page();
            }

            // Cargar información del usuario
            var usuario = await _context.Usuarios.FindAsync(userId.Value);
            if (usuario == null)
            {
                MensajeError = "No se pudo encontrar la información del usuario.";
                return Page();
            }

            // Mapear datos del usuario al ViewModel
            Usuario = new UsuarioViewModel
            {
                Id = usuario.IdUsuario,
                NombreCompleto = usuario.Nombre ?? "", // Ajustado según la estructura real
                Email = usuario.Correo ?? "", // Ajustado según la estructura real
                Telefono = usuario.Telefono ?? ""
                // Eliminamos la referencia a Direccion ya que no existe en la clase Usuario
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Verificar si el usuario está autenticado
            var userId = HttpContext.Session.GetInt32("UserId");
            UsuarioAutenticado = userId.HasValue && userId.Value > 0;

            if (!UsuarioAutenticado)
            {
                return RedirectToPage("/Cliente/Login");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Obtener el usuario de la base de datos
                var usuario = await _context.Usuarios.FindAsync(userId.Value);
                if (usuario == null)
                {
                    MensajeError = "No se pudo encontrar la información del usuario.";
                    return Page();
                }

                // Actualizar los datos del usuario
                usuario.Nombre = Usuario.NombreCompleto; // Ajustado según la estructura real
                usuario.Telefono = Usuario.Telefono;
                // Eliminamos la referencia a Direccion ya que no existe en la clase Usuario

                // Guardar cambios en la base de datos
                await _context.SaveChangesAsync();

                MensajeExito = "La información se ha actualizado correctamente.";
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al actualizar la información: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCambiarPasswordAsync()
        {
            // Verificar si el usuario está autenticado
            var userId = HttpContext.Session.GetInt32("UserId");
            UsuarioAutenticado = userId.HasValue && userId.Value > 0;

            if (!UsuarioAutenticado)
            {
                return RedirectToPage("/Cliente/Login");
            }

            // Validar el modelo de cambio de contraseña
            if (!ModelState.IsValid)
            {
                // Cargar información del usuario para mostrar el formulario completo
                await CargarInformacionUsuario(userId.Value);
                return Page();
            }

            try
            {
                // Obtener el usuario de la base de datos
                var usuario = await _context.Usuarios.FindAsync(userId.Value);
                if (usuario == null)
                {
                    MensajeError = "No se pudo encontrar la información del usuario.";
                    await CargarInformacionUsuario(userId.Value);
                    return Page();
                }

                // Verificar la contraseña actual
                if (usuario.Contrasena != CambioPassword.PasswordActual) // Ajustado según la estructura real
                {
                    MensajeError = "La contraseña actual es incorrecta.";
                    await CargarInformacionUsuario(userId.Value);
                    return Page();
                }

                // Verificar que las nuevas contraseñas coincidan
                if (CambioPassword.NuevaPassword != CambioPassword.ConfirmarPassword)
                {
                    MensajeError = "Las nuevas contraseñas no coinciden.";
                    await CargarInformacionUsuario(userId.Value);
                    return Page();
                }

                // Actualizar la contraseña
                usuario.Contrasena = CambioPassword.NuevaPassword; // Ajustado según la estructura real

                // Guardar cambios en la base de datos
                await _context.SaveChangesAsync();

                MensajeExito = "La contraseña se ha actualizado correctamente.";
            }
            catch (Exception ex)
            {
                MensajeError = $"Error al cambiar la contraseña: {ex.Message}";
            }

            // Recargar información del usuario
            await CargarInformacionUsuario(userId.Value);
            return Page();
        }

        private async Task CargarInformacionUsuario(int userId)
        {
            var usuario = await _context.Usuarios.FindAsync(userId);
            if (usuario != null)
            {
                Usuario = new UsuarioViewModel
                {
                    Id = usuario.IdUsuario,
                    NombreCompleto = usuario.Nombre ?? "", // Ajustado según la estructura real
                    Email = usuario.Correo ?? "", // Ajustado según la estructura real
                    Telefono = usuario.Telefono ?? ""
                    // Eliminamos la referencia a Direccion ya que no existe en la clase Usuario
                };
            }
        }
    }

    public class UsuarioViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre completo es requerido")]
        [Display(Name = "Nombre Completo")]
        public string NombreCompleto { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido")]
        [Display(Name = "Correo Electrónico")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "El formato del teléfono no es válido")]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; }

        // Mantenemos Direccion en el ViewModel aunque no exista en la clase Usuario
        // para que la vista pueda seguir funcionando
        [Display(Name = "Dirección")]
        public string? Direccion { get; set; }
    }

    public class CambioPasswordViewModel
    {
        [Required(ErrorMessage = "La contraseña actual es requerida")]
        [Display(Name = "Contraseña Actual")]
        public string PasswordActual { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es requerida")]
        [StringLength(100, ErrorMessage = "La {0} debe tener al menos {2} caracteres de longitud.", MinimumLength = 6)]
        [Display(Name = "Nueva Contraseña")]
        public string NuevaPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "La confirmación de contraseña es requerida")]
        [Compare("NuevaPassword", ErrorMessage = "La nueva contraseña y la confirmación no coinciden.")]
        [Display(Name = "Confirmar Nueva Contraseña")]
        public string ConfirmarPassword { get; set; } = string.Empty;
    }
}
