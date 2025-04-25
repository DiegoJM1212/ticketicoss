using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ticketicos.Services;

namespace ticketicos.Pages.Admin
{
    public class NewslettersModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly EmailService _emailService;

        public NewslettersModel(IConfiguration configuration, EmailService emailService)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

        [BindProperty]
        public string Asunto { get; set; } = string.Empty;

        [BindProperty]
        public string Contenido { get; set; } = string.Empty;

        [TempData]
        public string? MensajeExito { get; set; } // Cambiado a string? (nullable)

        [TempData]
        public string? MensajeError { get; set; } // Cambiado a string? (nullable)

        public IActionResult OnGet()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? userType = HttpContext.Session.GetString("UserType");
            if (!userId.HasValue || userType != "admin")
            {
                return RedirectToPage("/Cliente/Login");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostEnviarNewsletterAsync()
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");
                if (!userId.HasValue || userType != "admin")
                {
                    return RedirectToPage("/Cliente/Login");
                }

                if (string.IsNullOrEmpty(Asunto) || string.IsNullOrEmpty(Contenido))
                {
                    TempData["MensajeError"] = "El asunto y el contenido son obligatorios.";
                    return Page();
                }

                // Obtener correos de todos los clientes
                List<string> correos = new List<string>();
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT correo FROM Usuarios WHERE tipo_usuario = 'cliente' AND estado = 'activo'";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                correos.Add(reader.GetString(0));
                            }
                        }
                    }
                }

                if (correos.Count == 0)
                {
                    TempData["MensajeError"] = "No hay clientes activos para enviar el newsletter.";
                    return Page();
                }

                // Enviar el newsletter a cada cliente
                foreach (var correo in correos)
                {
                    await _emailService.SendNewsletterAsync(correo, Asunto, Contenido);
                }

                // Registrar la acción en RegistroAcciones
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = @"
                        INSERT INTO RegistroAcciones (id_usuario_admin, id_usuario_afectado, tipo_accion, detalle_accion, fecha_accion)
                        VALUES (@IdAdmin, NULL, 'envio_newsletter', @DetalleAccion, GETDATE())";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdAdmin", userId.Value);
                        command.Parameters.AddWithValue("@DetalleAccion", $"Envío de newsletter: {Asunto}");
                        await command.ExecuteNonQueryAsync();
                    }
                }

                TempData["MensajeExito"] = "Newsletter enviado correctamente a todos los clientes.";
            }
            catch (Exception ex)
            {
                TempData["MensajeError"] = $"Error al enviar el newsletter: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}