using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ticketicos.Pages.Admin
{
    public class ConfiguracionesModel : PageModel
    {
        private readonly ILogger<ConfiguracionesModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public ConfiguracionesModel(ILogger<ConfiguracionesModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
            Configuraciones = new List<ConfigViewModel>();
        }

        public List<ConfigViewModel> Configuraciones { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            // Verificar si el usuario está autenticado y es administrador
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? userType = HttpContext.Session.GetString("UserType");

            if (!userId.HasValue || userType != "admin")
            {
                _logger.LogWarning("Intento de acceso no autorizado a la página de configuraciones");
                return RedirectToPage("/Index");
            }

            await CargarConfiguracionesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostCrearConfigAsync(string Clave, string Valor)
        {
            // Verificar si el usuario está autenticado y es administrador
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? userType = HttpContext.Session.GetString("UserType");

            if (!userId.HasValue || userType != "admin")
            {
                _logger.LogWarning("Intento de acceso no autorizado para crear configuración");
                return RedirectToPage("/Index");
            }

            if (!Clave.StartsWith("Custom"))
            {
                ErrorMessage = "Las claves personalizadas deben comenzar con 'Custom_'.";
                await CargarConfiguracionesAsync();
                return Page();
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "INSERT INTO Configuraciones (clave, valor) VALUES (@Clave, @Valor)";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Clave", Clave);
                        command.Parameters.AddWithValue("@Valor", Valor);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Configuración creada exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear configuración");
                ErrorMessage = "Error al crear la configuración. Intenta nuevamente.";
            }
            await CargarConfiguracionesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostActualizarConfigAsync(int IdConfig, string Clave, string Valor)
        {
            // Verificar si el usuario está autenticado y es administrador
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? userType = HttpContext.Session.GetString("UserType");

            if (!userId.HasValue || userType != "admin")
            {
                _logger.LogWarning("Intento de acceso no autorizado para actualizar configuración");
                return RedirectToPage("/Index");
            }

            // Validaciones para evitar configuraciones que rompan los correos
            if (Clave == "MailgunDomain" || Clave == "MailgunApiKey")
            {
                if (string.IsNullOrWhiteSpace(Valor))
                {
                    ErrorMessage = $"{Clave} no puede estar vacío.";
                    await CargarConfiguracionesAsync();
                    return Page();
                }
            }

            // Validar plantillas con placeholders
            if (Clave.Contains("Text") || Clave.Contains("Subject"))
            {
                int placeholderCount = Valor.Count(f => f == '{');
                if ((Clave == "InvoiceSubject" || Clave == "InvoiceText") && placeholderCount != 1)
                {
                    ErrorMessage = $"{Clave} debe tener exactamente un placeholder {{0}}.";
                    await CargarConfiguracionesAsync();
                    return Page();
                }
                if ((Clave == "TicketsSubject" || Clave == "TicketsText") && placeholderCount != 1)
                {
                    ErrorMessage = $"{Clave} debe tener exactamente un placeholder {{0}}.";
                    await CargarConfiguracionesAsync();
                    return Page();
                }
                if ((Clave == "PromoCodeText") && placeholderCount != 1)
                {
                    ErrorMessage = $"{Clave} debe tener exactamente un placeholder {{0}}.";
                    await CargarConfiguracionesAsync();
                    return Page();
                }
                if ((Clave == "TwoFactorText") && placeholderCount != 1)
                {
                    ErrorMessage = $"{Clave} debe tener exactamente un placeholder {{0}}.";
                    await CargarConfiguracionesAsync();
                    return Page();
                }
                if ((Clave == "PasswordResetText") && placeholderCount != 1)
                {
                    ErrorMessage = $"{Clave} debe tener exactamente un placeholder {{0}}.";
                    await CargarConfiguracionesAsync();
                    return Page();
                }
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "UPDATE Configuraciones SET clave = @Clave, valor = @Valor WHERE id_config = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", IdConfig);
                        command.Parameters.AddWithValue("@Clave", Clave);
                        command.Parameters.AddWithValue("@Valor", Valor);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Configuración actualizada exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar configuración");
                ErrorMessage = "Error al actualizar la configuración. Intenta nuevamente.";
            }
            await CargarConfiguracionesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostEliminarConfigAsync(int IdConfig)
        {
            // Verificar si el usuario está autenticado y es administrador
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? userType = HttpContext.Session.GetString("UserType");

            if (!userId.HasValue || userType != "admin")
            {
                _logger.LogWarning("Intento de acceso no autorizado para eliminar configuración");
                return RedirectToPage("/Index");
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "DELETE FROM Configuraciones WHERE id_config = @Id AND clave LIKE 'Custom_%'";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", IdConfig);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Configuración eliminada exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar configuración");
                ErrorMessage = "Error al eliminar la configuración. Intenta nuevamente.";
            }
            await CargarConfiguracionesAsync();
            return Page();
        }

        private async Task CargarConfiguracionesAsync()
        {
            Configuraciones.Clear();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT id_config, clave, valor FROM Configuraciones";
                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Configuraciones.Add(new ConfigViewModel
                                {
                                    Id = reader.GetInt32(0),
                                    Clave = reader.GetString(1),
                                    Valor = reader.GetString(2)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar configuraciones");
                ErrorMessage = "Error al cargar las configuraciones. Intenta nuevamente.";
            }
        }
    }

    public class ConfigViewModel
    {
        public int Id { get; set; }
        public string Clave { get; set; } = string.Empty;
        public string Valor { get; set; } = string.Empty;
    }
}