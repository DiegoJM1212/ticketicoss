using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ticketicos.Data;
using ticketicos.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ticketicos.Pages
{
    public class IndexModel : PageModel
    {
        private readonly VerEventoContext _context;
        private readonly string _connectionString;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(VerEventoContext context, IConfiguration configuration, ILogger<IndexModel> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Eventos = new List<Evento>();
            EventosActuales = new List<Evento>();
            EventosProximos = new List<Evento>();
        }

        public List<Evento> Eventos { get; set; }
        public List<Evento> EventosActuales { get; set; }
        public List<Evento> EventosProximos { get; set; }

        public async Task<IActionResult> OnGetAsync(string categoria, string nombreEvento)
        {
            try
            {
                _logger.LogInformation("Iniciando carga de eventos con filtros: categoria={Categoria}, nombreEvento={NombreEvento}", categoria, nombreEvento);

                var hoy = DateTime.Today;

                // Consulta general de eventos con filtros si existen
                var query = _context.Eventos.AsQueryable();

                if (!string.IsNullOrEmpty(categoria))
                {
                    query = query.Where(e => e.Categoria == categoria);
                }

                if (!string.IsNullOrEmpty(nombreEvento))
                {
                    query = query.Where(e => e.Nombre.Contains(nombreEvento));
                }

                // Ejecuta la consulta principal
                Eventos = await query.ToListAsync();
                _logger.LogInformation("Eventos cargados: {Count} eventos encontrados", Eventos.Count);

                // Eventos actuales: este mes o el siguiente
                EventosActuales = await _context.Eventos
                    .Where(e => e.Fecha.Month == hoy.Month || e.Fecha.Month == hoy.AddMonths(1).Month)
                    .OrderBy(e => e.Fecha)
                    .ToListAsync();
                _logger.LogInformation("Eventos actuales cargados: {Count} eventos", EventosActuales.Count);

                // Eventos próximos: eventos que ocurren exactamente dentro de dos meses
                EventosProximos = await _context.Eventos
                    .Where(e => e.Fecha.Month == hoy.AddMonths(2).Month)
                    .OrderBy(e => e.Fecha)
                    .ToListAsync();
                _logger.LogInformation("Eventos próximos cargados: {Count} eventos", EventosProximos.Count);

                return Page();
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Error de conexión con la base de datos al cargar eventos. Código: {ErrorCode}, Mensaje: {ErrorMessage}", ex.Number, ex.Message);
                // Mostrar un mensaje genérico al usuario
                TempData["ErrorMessage"] = "No se pudo conectar con la base de datos. Por favor, intenta de nuevo más tarde.";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al cargar eventos: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Ocurrió un error al cargar los eventos. Por favor, intenta de nuevo.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostChatbotAsync([FromBody] ChatbotRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Message))
                {
                    _logger.LogWarning("Mensaje de chatbot vacío o nulo");
                    return new JsonResult(new ChatbotResponse { Response = "Por favor, escribe un mensaje válido." });
                }

                string message = request.Message.ToLower().Trim();
                _logger.LogInformation("Procesando mensaje de chatbot: {Message}", message);
                string response = await GetResponseAsync(message);

                return new JsonResult(new ChatbotResponse { Response = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al procesar mensaje de chatbot: {Message}", request?.Message);
                return new JsonResult(new ChatbotResponse { Response = $"Error al procesar tu mensaje: {ex.Message}" });
            }
        }

        private async Task<string> GetResponseAsync(string message)
        {
            try
            {
                // Consultar PreguntasFrecuentes
                using (var connection = new SqlConnection(_connectionString))
                {
                    try
                    {
                        _logger.LogInformation("Intentando abrir conexión a la base de datos con cadena: {ConnectionString}", _connectionString);
                        await connection.OpenAsync();
                        _logger.LogInformation("Conexión a la base de datos abierta correctamente");

                        // Consulta alternativa usando CAST para manejar columnas TEXT
                        var query = @"
                            SELECT respuesta
                            FROM PreguntasFrecuentes
                            WHERE LOWER(CAST(pregunta AS NVARCHAR(MAX))) LIKE @Pregunta AND Habilitado = 1";
                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@Pregunta", $"%{message}%");
                            _logger.LogInformation("Ejecutando consulta SQL: {Query} con parámetro: {Parameter}", query, message);

                            var result = await command.ExecuteScalarAsync();
                            if (result != null && result != DBNull.Value)
                            {
                                _logger.LogInformation("Respuesta encontrada en PreguntasFrecuentes para: {Message}", message);
                                return result.ToString();
                            }
                            else
                            {
                                _logger.LogInformation("No se encontró respuesta en PreguntasFrecuentes para: {Message}", message);
                            }
                        }
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogError(ex, "Error de base de datos al consultar PreguntasFrecuentes. Código: {ErrorCode}, Mensaje: {ErrorMessage}", ex.Number, ex.Message);
                        return $"Error al consultar la base de datos: {ex.Message}. Intenta de nuevo.";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inesperado al consultar PreguntasFrecuentes");
                        return $"Error inesperado al consultar la base de datos: {ex.Message}. Intenta de nuevo.";
                    }
                }

                // Respuestas predefinidas para servicios
                if (message.Contains("consultar saldo") || message.Contains("mi saldo"))
                {
                    int? userId = HttpContext.Session.GetInt32("UserId");
                    if (userId.HasValue)
                    {
                        try
                        {
                            decimal saldo = await GetSaldoAsync(userId.Value);
                            _logger.LogInformation("Saldo consultado para usuario {UserId}: {Saldo}", userId, saldo);
                            return $"Tu saldo en la billetera virtual es ₡{saldo}.";
                        }
                        catch (SqlException ex)
                        {
                            _logger.LogError(ex, "Error al consultar saldo para usuario {UserId}", userId);
                            return "Error al consultar tu saldo. Intenta de nuevo.";
                        }
                    }
                    _logger.LogWarning("Intento de consultar saldo sin sesión activa");
                    return "Por favor, inicia sesión para consultar tu saldo.";
                }
                else if (message.Contains("recomendar eventos") || message.Contains("qué eventos hay"))
                {
                    try
                    {
                        var eventos = await GetEventosAsync();
                        if (eventos.Any())
                        {
                            _logger.LogInformation("Eventos recomendados: {Eventos}", string.Join(", ", eventos));
                            return "Te recomiendo estos eventos: " + string.Join(", ", eventos);
                        }
                        _logger.LogInformation("No se encontraron eventos para recomendar");
                        return "No hay eventos disponibles en este momento.";
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogError(ex, "Error al consultar eventos");
                        return "Error al consultar eventos. Intenta de nuevo.";
                    }
                }
                else if (message.Contains("cómo comprar") || message.Contains("comprar boletos"))
                {
                    _logger.LogInformation("Respuesta predefinida para 'cómo comprar boletos'");
                    return @"Para comprar boletos:
                    1. Ve a la sección 'Eventos' y selecciona un evento.
                    2. Elige tu bloque y asientos.
                    3. Usa tu billetera virtual o una tarjeta en la página de pago.";
                }
                else if (message.Contains("código promocional") || message.Contains("usar un código"))
                {
                    _logger.LogInformation("Respuesta predefinida para 'código promocional'");
                    return @"Para usar un código promocional:
                    1. En la página de pago, ingresa el código en el campo 'Código Promocional'.
                    2. Haz clic en 'Aplicar' para ver el descuento.
                    Ejemplo: Usa 'DESCUENTO10' para ₡3,000 de descuento.";
                }

                _logger.LogInformation("No se encontró respuesta para: {Message}", message);
                return "Lo siento, no entendí tu pregunta. Prueba con: ¿Cómo comprar boletos?, Consultar saldo, Recomendar eventos.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al procesar mensaje: {Message}", message);
                return $"Error al procesar tu mensaje: {ex.Message}";
            }
        }

        private async Task<decimal> GetSaldoAsync(int userId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT SUM(Saldo) FROM TarjetasUsuario WHERE IdUsuario = @IdUsuario";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdUsuario", userId);
                        var result = await command.ExecuteScalarAsync();
                        return result != null && result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                    }
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Error al consultar saldo para usuario {UserId}. Código: {ErrorCode}, Mensaje: {ErrorMessage}", userId, ex.Number, ex.Message);
                throw;
            }
        }

        private async Task<List<string>> GetEventosAsync()
        {
            var eventos = new List<string>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT nombre FROM Eventos WHERE fecha >= GETDATE() AND estado = 'activo' ORDER BY fecha";
                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                eventos.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Error al consultar eventos. Código: {ErrorCode}, Mensaje: {ErrorMessage}", ex.Number, ex.Message);
                throw;
            }
            return eventos;
        }
    }

    public class ChatbotRequest
    {
        public string Message { get; set; }
    }

    public class ChatbotResponse
    {
        public string Response { get; set; }
    }
}