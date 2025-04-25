using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ticketicos.Pages.Cliente
{
    public class PreventaModel : PageModel
    {
        private readonly ILogger<PreventaModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public PreventaModel(ILogger<PreventaModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");

            Eventos = new List<EventoPreventaViewModel>();
            TieneAccesoPreventa = false;
        }

        public List<EventoPreventaViewModel> Eventos { get; set; }
        public bool TieneAccesoPreventa { get; set; }
        public string MensajeAcceso { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Iniciando carga de eventos en preventa");

                int? userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    _logger.LogWarning("Usuario no autenticado, redirigiendo a login.");
                    MensajeAcceso = "Por favor, inicia sesión para acceder a las preventas.";
                    return RedirectToPage("/Cliente/Login");
                }

                _logger.LogInformation("Usuario autenticado con ID: {UserId}", userId.Value);

                // Verificar si el usuario tiene tarjeta American Express
                bool tieneAmex = await VerificarTarjetaAmexAsync(userId.Value);
                TieneAccesoPreventa = tieneAmex;

                if (TieneAccesoPreventa)
                {
                    await CargarEventosPreventaAsync();
                    if (Eventos.Any())
                    {
                        MensajeAcceso = "¡Tienes acceso exclusivo a todas las preventas con tu tarjeta American Express!";
                    }
                    else
                    {
                        MensajeAcceso = "No hay eventos en preventa disponibles en este momento.";
                        _logger.LogWarning("No se encontraron eventos en preventa para el usuario {UserId}", userId.Value);
                    }
                }
                else
                {
                    MensajeAcceso = "Agrega una tarjeta American Express a tu billetera para acceder a preventas exclusivas.";
                    _logger.LogInformation("Usuario {UserId} no tiene tarjeta American Express", userId.Value);
                }

                return Page();
            }
            catch (Exception ex)
            {
                int? userId = HttpContext.Session.GetInt32("UserId"); // Volver a obtener userId en el catch
                _logger.LogError(ex, "Error general al cargar eventos en preventa para usuario {UserId}", userId.HasValue ? userId.Value : 0);
                MensajeAcceso = "Error al cargar eventos. Por favor, intenta nuevamente.";
                return Page();
            }
        }

        private async Task<bool> VerificarTarjetaAmexAsync(int userId)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT COUNT(1) 
                        FROM TarjetasUsuario 
                        WHERE IdUsuario = @UserId 
                        AND TipoTarjeta = 'American Express'";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        var result = await command.ExecuteScalarAsync();
                        int count = result != null ? Convert.ToInt32(result) : 0;

                        _logger.LogInformation("Usuario {UserId} tiene {Count} tarjetas American Express", userId, count);
                        if (count == 0)
                        {
                            _logger.LogWarning("No se encontraron tarjetas American Express para el usuario {UserId}", userId);
                        }
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar tarjeta American Express para usuario {UserId}: {ErrorMessage}", userId, ex.Message);
                return false;
            }
        }

        private async Task CargarEventosPreventaAsync()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    _logger.LogInformation("Conexión a la base de datos abierta para cargar eventos en preventa");

                    string query = @"
                        SELECT e.id_evento, e.nombre, e.fecha, e.lugar, e.imagen_url, 
                               l.nombre AS lugar_nombre, 
                               p.id_preventa, p.fecha_inicio, p.fecha_fin, 
                               ISNULL(p.limite_boletos_por_usuario, 2) AS limite_boletos
                        FROM Eventos e
                        INNER JOIN Preventa p ON e.id_evento = p.id_evento
                        LEFT JOIN Lugares l ON e.id_lugar = l.id_lugar
                        WHERE e.estado = 'activo' 
                        AND p.estado = 'activa'
                        AND p.fecha_inicio <= GETDATE()
                        AND p.fecha_fin >= GETDATE()
                        ORDER BY e.fecha ASC";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (!reader.HasRows)
                            {
                                _logger.LogWarning("No se encontraron eventos en preventa que cumplan los criterios");
                            }
                            else
                            {
                                _logger.LogInformation("Eventos en preventa encontrados, procesando filas");
                            }

                            while (await reader.ReadAsync())
                            {
                                var evento = new EventoPreventaViewModel
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("id_evento")),
                                    Nombre = reader.IsDBNull(reader.GetOrdinal("nombre")) ? "Sin nombre" : reader.GetString(reader.GetOrdinal("nombre")),
                                    Fecha = reader.GetDateTime(reader.GetOrdinal("fecha")),
                                    Lugar = reader.IsDBNull(reader.GetOrdinal("lugar")) ? "Sin lugar" : reader.GetString(reader.GetOrdinal("lugar")),
                                    LugarNombre = reader.IsDBNull(reader.GetOrdinal("lugar_nombre")) ? reader.GetString(reader.GetOrdinal("lugar")) : reader.GetString(reader.GetOrdinal("lugar_nombre")),
                                    Hora = reader.GetDateTime(reader.GetOrdinal("fecha")).ToString("HH:mm"), // Derivar hora de fecha
                                    LimiteBoletos = reader.GetInt32(reader.GetOrdinal("limite_boletos"))
                                };

                                // Procesar la imagen
                                if (!reader.IsDBNull(reader.GetOrdinal("imagen_url")))
                                {
                                    string imagenUrl = reader.GetString(reader.GetOrdinal("imagen_url"));
                                    if (imagenUrl.StartsWith("http"))
                                    {
                                        evento.ImagenUrl = imagenUrl;
                                    }
                                    else
                                    {
                                        evento.ImagenUrl = $"/{imagenUrl.TrimStart('/')}";
                                    }
                                }
                                else
                                {
                                    evento.ImagenUrl = "/images/evento-default.jpg";
                                }

                                Eventos.Add(evento);
                                _logger.LogInformation("Evento en preventa cargado: {EventoNombre}, ID: {EventoId}, Fecha: {Fecha}, LimiteBoletos: {LimiteBoletos}",
                                    evento.Nombre, evento.Id, evento.Fecha, evento.LimiteBoletos);
                            }
                        }
                    }
                }

                _logger.LogInformation("Se cargaron {EventoCount} eventos en preventa", Eventos.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar eventos en preventa: {ErrorMessage}", ex.Message);
                Eventos = new List<EventoPreventaViewModel>();
            }
        }
    }

    public class EventoPreventaViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Hora { get; set; } = string.Empty;
        public string Lugar { get; set; } = string.Empty;
        public string LugarNombre { get; set; } = string.Empty;
        public string ImagenUrl { get; set; } = string.Empty;
        public int LimiteBoletos { get; set; } = 2;
    }
}
