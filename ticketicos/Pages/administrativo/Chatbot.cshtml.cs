using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace ticketicos.Pages.Admin
{
    public class ChatbotModel : PageModel
    {
        private readonly string? _connectionString;
        private readonly ILogger<ChatbotModel> _logger;

        public ChatbotModel(IConfiguration configuration, ILogger<ChatbotModel> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
            Preguntas = new List<PreguntaFrecuente>();
            NuevaPregunta = new PreguntaFrecuente();

            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
            }
        }

        public List<PreguntaFrecuente> Preguntas { get; set; }
        [BindProperty]
        public PreguntaFrecuente NuevaPregunta { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Verificar si el usuario está logueado y es admin
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? userType = HttpContext.Session.GetString("UserType");
            if (!userId.HasValue || userType != "admin")
            {
                return RedirectToPage("/Cliente/Login");
            }

            await CargarPreguntasAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAgregarAsync()
        {
            // Verificar si el usuario está logueado y es admin
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? userType = HttpContext.Session.GetString("UserType");
            if (!userId.HasValue || userType != "admin")
            {
                return RedirectToPage("/Cliente/Login");
            }

            if (!ModelState.IsValid)
            {
                await CargarPreguntasAsync();
                return Page();
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = @"
                        IF @Id = 0
                            INSERT INTO PreguntasFrecuentes (pregunta, respuesta, fecha, Habilitado)
                            VALUES (@Pregunta, @Respuesta, @Fecha, @Habilitado)
                        ELSE
                            UPDATE PreguntasFrecuentes
                            SET pregunta = @Pregunta, respuesta = @Respuesta
                            WHERE id_pregunta = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", NuevaPregunta.Id);
                        command.Parameters.AddWithValue("@Pregunta", NuevaPregunta.Pregunta);
                        command.Parameters.AddWithValue("@Respuesta", NuevaPregunta.Respuesta ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Fecha", DateTime.Today);
                        command.Parameters.AddWithValue("@Habilitado", true);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar pregunta");
                ModelState.AddModelError(string.Empty, "Error al guardar la pregunta.");
                await CargarPreguntasAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnGetEditarAsync(int id)
        {
            // Verificar si el usuario está logueado y es admin
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? userType = HttpContext.Session.GetString("UserType");
            if (!userId.HasValue || userType != "admin")
            {
                return RedirectToPage("/Cliente/Login");
            }

            await CargarPreguntasAsync();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "SELECT id_pregunta, pregunta, respuesta FROM PreguntasFrecuentes WHERE id_pregunta = @Id";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            NuevaPregunta = new PreguntaFrecuente
                            {
                                Id = reader.GetInt32(0),
                                Pregunta = reader.GetString(1),
                                Respuesta = reader.IsDBNull(2) ? null : reader.GetString(2)
                            };
                        }
                    }
                }
            }
            return Page();
        }

        public async Task<IActionResult> OnGetHabilitarAsync(int id)
        {
            // Verificar si el usuario está logueado y es admin
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? userType = HttpContext.Session.GetString("UserType");
            if (!userId.HasValue || userType != "admin")
            {
                return RedirectToPage("/Cliente/Login");
            }

            await ToggleHabilitadoAsync(id, true);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetDeshabilitarAsync(int id)
        {
            // Verificar si el usuario está logueado y es admin
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? userType = HttpContext.Session.GetString("UserType");
            if (!userId.HasValue || userType != "admin")
            {
                return RedirectToPage("/Cliente/Login");
            }

            await ToggleHabilitadoAsync(id, false);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetEliminarAsync(int id)
        {
            // Verificar si el usuario está logueado y es admin
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? userType = HttpContext.Session.GetString("UserType");
            if (!userId.HasValue || userType != "admin")
            {
                return RedirectToPage("/Cliente/Login");
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "DELETE FROM PreguntasFrecuentes WHERE id_pregunta = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al eliminar pregunta {id}");
            }
            return RedirectToPage();
        }

        private async Task CargarPreguntasAsync()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT id_pregunta, pregunta, respuesta, Habilitado FROM PreguntasFrecuentes";
                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            Preguntas.Clear();
                            while (await reader.ReadAsync())
                            {
                                Preguntas.Add(new PreguntaFrecuente
                                {
                                    Id = reader.GetInt32(0),
                                    Pregunta = reader.GetString(1),
                                    Respuesta = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Habilitado = reader.GetBoolean(3)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar preguntas");
            }
        }

        private async Task ToggleHabilitadoAsync(int id, bool habilitado)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "UPDATE PreguntasFrecuentes SET Habilitado = @Habilitado WHERE id_pregunta = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        command.Parameters.AddWithValue("@Habilitado", habilitado);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al cambiar estado de pregunta {id}");
            }
        }
    }

    public class PreguntaFrecuente
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "La pregunta es obligatoria")]
        public string Pregunta { get; set; } = string.Empty;
        public string? Respuesta { get; set; }
        public bool Habilitado { get; set; }
    }
}