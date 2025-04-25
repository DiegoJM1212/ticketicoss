using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ticketicos.Pages
{
    public class ChatbotWebhookModel : PageModel
    {
        private readonly string? _connectionString; // Hacer nullable
        private readonly ILogger<ChatbotWebhookModel> _logger;

        public ChatbotWebhookModel(IConfiguration configuration, ILogger<ChatbotWebhookModel> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;

            // Validar que _connectionString no sea null
            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                string requestBody;
                using (var reader = new System.IO.StreamReader(Request.Body))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                dynamic? request = Newtonsoft.Json.JsonConvert.DeserializeObject(requestBody);
                string? intent = request?.queryResult?.intent?.displayName?.ToString();
                string? message = request?.queryResult?.queryText?.ToString()?.ToLower()?.Trim();

                string response = await GetResponseAsync(intent, message);
                return new JsonResult(new { fulfillmentText = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en webhook");
                return new JsonResult(new { fulfillmentText = "Error al procesar tu mensaje." });
            }
        }

        private async Task<string> GetResponseAsync(string? intent, string? message)
        {
            if (string.IsNullOrEmpty(intent) || string.IsNullOrEmpty(message))
            {
                return "Por favor, escribe un mensaje válido.";
            }

            // Consultar PreguntasFrecuentes
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = @"
                    SELECT respuesta
                    FROM PreguntasFrecuentes
                    WHERE LOWER(pregunta) LIKE @Pregunta AND Habilitado = 1";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Pregunta", $"%{message}%");
                    var result = await command.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        return result?.ToString() ?? string.Empty;

                    }
                }
            }

            // Respuestas basadas en intenciones
            if (intent == "ConsultarSaldo")
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                if (userId.HasValue)
                {
                    decimal saldo = await GetSaldoAsync(userId.Value);
                    return $"Tu saldo en la billetera virtual es ₡{saldo}.";
                }
                return "Por favor, inicia sesión para consultar tu saldo.";
            }
            else if (intent == "RecomendarEventos")
            {
                var eventos = await GetEventosAsync();
                if (eventos.Any())
                {
                    return "Te recomiendo estos eventos: " + string.Join(", ", eventos);
                }
                return "No hay eventos disponibles en este momento.";
            }
            else if (intent == "AyudaCompra")
            {
                return @"Para comprar boletos:
                1. Ve a la sección 'Eventos' y selecciona un evento.
                2. Elige tu bloque y asientos.
                3. Usa tu billetera virtual o una tarjeta en la página de pago.";
            }
            else if (intent == "UsarCodigo")
            {
                return @"Para usar un código promocional:
                1. En la página de pago, ingresa el código en el campo 'Código Promocional'.
                2. Haz clic en 'Aplicar' para ver el descuento.
                Ejemplo: Usa 'DESCUENTO10' para ₡3,000 de descuento.";
            }

            return "Lo siento, no entendí tu pregunta. Prueba con: ¿Cómo comprar boletos?, Consultar saldo, Recomendar eventos.";
        }

        private async Task<decimal> GetSaldoAsync(int userId)
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

        private async Task<List<string>> GetEventosAsync()
        {
            var eventos = new List<string>();
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
            return eventos;
        }
    }
}