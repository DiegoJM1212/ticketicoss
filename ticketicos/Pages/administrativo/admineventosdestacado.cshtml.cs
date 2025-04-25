using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Tickicos.Pages.Admin
{
    public class AdminEventosDestacadoModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminEventosDestacadoModel> _logger;
        private readonly string _connectionString;

        public AdminEventosDestacadoModel(IConfiguration configuration, ILogger<AdminEventosDestacadoModel> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
            Eventos = new List<EventoViewModel>();
        }

        public List<EventoViewModel> Eventos { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");
                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de eventos destacados");
                    return RedirectToPage("/Cliente/Login");
                }

                await CargarEventosAsync();
                _logger.LogInformation("Se cargaron {Count} eventos activos", Eventos.Count);
                return Page();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Error de base de datos al cargar eventos destacados");
                ErrorMessage = $"Error de base de datos al cargar los eventos: {ex.Message}";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al cargar eventos destacados");
                ErrorMessage = $"Error inesperado al cargar los eventos: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostToggleDestacadoAsync(int id, bool esDestacado)
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");
                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de eventos destacados");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    if (esDestacado)
                    {
                        string deleteQuery = @"
                            DELETE FROM EventosDestacados
                            WHERE id_evento = @IdEvento";
                        using (var command = new SqlCommand(deleteQuery, connection))
                        {
                            command.Parameters.AddWithValue("@IdEvento", id);
                            int rowsAffected = await command.ExecuteNonQueryAsync();
                            _logger.LogInformation("Eliminado evento destacado con id_evento {IdEvento}, filas afectadas: {RowsAffected}", id, rowsAffected);
                        }
                        SuccessMessage = "Evento removido de destacados exitosamente.";
                    }
                    else
                    {
                        string insertQuery = @"
                            INSERT INTO EventosDestacados (id_evento, activo, fecha_destacado)
                            VALUES (@IdEvento, 1, SYSDATETIME())";
                        using (var command = new SqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@IdEvento", id);
                            int rowsAffected = await command.ExecuteNonQueryAsync();
                            _logger.LogInformation("Insertado evento destacado con id_evento {IdEvento}, filas afectadas: {RowsAffected}", id, rowsAffected);
                        }
                        SuccessMessage = "Evento marcado como destacado exitosamente.";
                    }
                }

                await CargarEventosAsync();
                _logger.LogInformation("Recargados {Count} eventos tras toggle", Eventos.Count);
                return Page();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Error de base de datos al actualizar estado de destacado para evento {Id}", id);
                ErrorMessage = $"Error de base de datos al actualizar el estado: {ex.Message}";
                await CargarEventosAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al actualizar estado de destacado para evento {Id}", id);
                ErrorMessage = $"Error inesperado al actualizar el estado: {ex.Message}";
                await CargarEventosAsync();
                return Page();
            }
        }

        private async Task CargarEventosAsync()
        {
            Eventos.Clear();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"
                    SELECT e.id_evento, e.nombre, e.fecha, e.lugar, e.imagen_url,
                           CASE WHEN ed.id_evento IS NOT NULL THEN 1 ELSE 0 END AS es_destacado
                    FROM Eventos e
                    LEFT JOIN EventosDestacados ed ON e.id_evento = ed.id_evento AND ed.activo = 1
                    WHERE e.estado = 'activo'";
                using (var command = new SqlCommand(query, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string imagenUrl;
                            try
                            {
                                if (!reader.IsDBNull(4) && !string.IsNullOrWhiteSpace(reader.GetString(4)))
                                {
                                    imagenUrl = reader.GetString(4);
                                    _logger.LogDebug("Imagen URL cargada para evento {IdEvento}: {ImagenUrl}", reader.GetInt32(0), imagenUrl);
                                }
                                else
                                {
                                    imagenUrl = "/images/evento-default.jpg";
                                    _logger.LogDebug("Usando imagen por defecto para evento {IdEvento}", reader.GetInt32(0));
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error al procesar imagen_url para evento {IdEvento}", reader.GetInt32(0));
                                imagenUrl = "/images/evento-default.jpg";
                            }

                            try
                            {
                                Eventos.Add(new EventoViewModel
                                {
                                    Id = reader.GetInt32(0),
                                    Nombre = reader.GetString(1),
                                    Fecha = reader.GetDateTime(2),
                                    Hora = reader.GetDateTime(2).ToString("HH:mm"),
                                    Lugar = reader.GetString(3),
                                    ImagenUrl = imagenUrl,
                                    EsDestacado = reader.GetInt32(5) == 1
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error al procesar fila para evento {IdEvento}", reader.GetInt32(0));
                                continue;
                            }
                        }
                    }
                }
            }
            _logger.LogInformation("Cargados {Count} eventos desde la base de datos", Eventos.Count);
        }
    }

    public class EventoViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Hora { get; set; } = string.Empty;
        public string Lugar { get; set; } = string.Empty;
        public string ImagenUrl { get; set; } = string.Empty;
        public bool EsDestacado { get; set; }
    }
}