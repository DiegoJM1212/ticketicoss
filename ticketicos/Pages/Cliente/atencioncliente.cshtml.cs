using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using ticketicos.Hubs;

namespace ticketicos.Pages.Cliente
{
    public class AtencionClienteModel : PageModel
    {
        private readonly ILogger<AtencionClienteModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly IHubContext<ChatHub> _chatHubContext;

        public AtencionClienteModel(
            ILogger<AtencionClienteModel> logger,
            IConfiguration configuration,
            IHubContext<ChatHub> chatHubContext)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _chatHubContext = chatHubContext ?? throw new ArgumentNullException(nameof(chatHubContext));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        public List<PreguntaFrecuenteViewModel> PreguntasFrecuentes { get; set; } = new List<PreguntaFrecuenteViewModel>();
        public List<TicketSoporteViewModel> TicketsUsuario { get; set; } = new List<TicketSoporteViewModel>();
        public List<MensajeChatViewModel> MensajesChat { get; set; } = new List<MensajeChatViewModel>();
        public int? IdTicketSeleccionado { get; set; }

        [BindProperty(SupportsGet = true)]
        public string TerminoBusqueda { get; set; } = string.Empty;

        public string MensajeError { get; set; } = string.Empty;

        public async Task OnGetAsync(int? ticketId)
        {
            try
            {
                _logger.LogInformation("Iniciando OnGetAsync con ticketId: {TicketId}", ticketId);

                int? userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    string? userIdStr = Request.Cookies["UserId"];
                    if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                    {
                        userId = cookieUserId;
                        HttpContext.Session.SetInt32("UserId", cookieUserId);
                        _logger.LogInformation($"Usuario {userId} autenticado desde cookie");
                    }
                }

                if (!userId.HasValue)
                {
                    _logger.LogWarning("Usuario no autenticado. Redirigiendo a login.");
                    Response.Redirect("~/Cliente/Login");
                    return;
                }

                await CargarPreguntasFrecuentesAsync();
                await CargarTicketsUsuarioAsync(userId.Value);

                if (ticketId.HasValue)
                {
                    _logger.LogInformation("Verificando ticket {TicketId} para usuario {UserId}", ticketId, userId);
                    using (var connection = new SqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();
                        string checkQuery = "SELECT COUNT(*) FROM Soporte WHERE id_ticket = @TicketId AND id_usuario = @UserId";
                        using (var command = new SqlCommand(checkQuery, connection))
                        {
                            command.Parameters.AddWithValue("@TicketId", ticketId.Value);
                            command.Parameters.AddWithValue("@UserId", userId.Value);
                            object? result = await command.ExecuteScalarAsync();
                            int count = result != null ? Convert.ToInt32(result) : 0;
                            if (count == 0)
                            {
                                _logger.LogWarning("Ticket {TicketId} no encontrado para usuario {UserId}", ticketId, userId);
                                MensajeError = "El ticket seleccionado no existe o no tienes acceso.";
                                return;
                            }
                        }
                    }

                    IdTicketSeleccionado = ticketId.Value;
                    await CargarMensajesChatAsync(ticketId.Value);
                }
                else if (TicketsUsuario.Any())
                {
                    IdTicketSeleccionado = TicketsUsuario[0].Id;
                    await CargarMensajesChatAsync(TicketsUsuario[0].Id);
                }
                _logger.LogInformation("OnGetAsync completado con éxito para ticketId: {TicketId}", ticketId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en OnGetAsync para ticket {TicketId}", ticketId);
                MensajeError = "Ocurrió un error al cargar la información. Por favor, intenta nuevamente.";
            }
        }

        public async Task<IActionResult> OnPostBuscarAsync()
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    string? userIdStr = Request.Cookies["UserId"];
                    if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                    {
                        userId = cookieUserId;
                        HttpContext.Session.SetInt32("UserId", cookieUserId);
                        _logger.LogInformation($"Usuario {userId} autenticado desde cookie");
                    }
                }

                if (!userId.HasValue)
                {
                    _logger.LogWarning("Usuario no autenticado. Redirigiendo a login.");
                    return RedirectToPage("~/Cliente/Login");
                }

                await CargarPreguntasFrecuentesAsync(TerminoBusqueda);
                await CargarTicketsUsuarioAsync(userId.Value);

                if (TicketsUsuario.Any())
                {
                    IdTicketSeleccionado = TicketsUsuario[0].Id;
                    await CargarMensajesChatAsync(TicketsUsuario[0].Id);
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar preguntas frecuentes");
                MensajeError = "Ocurrió un error al realizar la búsqueda. Por favor, intenta nuevamente.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostEnviarMensajeAsync([FromBody] NuevoMensajeModel nuevoMensaje)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nuevoMensaje?.Mensaje))
                {
                    _logger.LogWarning("Mensaje vacío recibido en OnPostEnviarMensajeAsync");
                    return new JsonResult(new { success = false, message = "El mensaje no puede estar vacío." });
                }

                int? userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    string? userIdStr = Request.Cookies["UserId"];
                    if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                    {
                        userId = cookieUserId;
                        HttpContext.Session.SetInt32("UserId", cookieUserId);
                        _logger.LogInformation($"Usuario {userId} autenticado desde cookie");
                    }
                }

                if (!userId.HasValue)
                {
                    _logger.LogWarning("Usuario no autenticado al intentar enviar mensaje");
                    return new JsonResult(new { success = false, message = "Debes iniciar sesión para enviar un mensaje." });
                }

                _logger.LogInformation("Usuario {UserId} intentando enviar mensaje: {Mensaje}", userId, nuevoMensaje.Mensaje);

                int ticketId = await ObtenerOCrearTicketAsync(userId.Value, nuevoMensaje.Mensaje);
                _logger.LogInformation("Ticket {TicketId} obtenido o creado para usuario {UserId}", ticketId, userId);

                bool mensajeEnviado = await EnviarMensajeAsync(ticketId, userId.Value, nuevoMensaje.Mensaje);
                if (mensajeEnviado)
                {
                    string senderName = await GetUserNameAsync(userId.Value);
                    await _chatHubContext.Clients.Group($"Ticket_{ticketId}")
                        .SendAsync("ReceiveMessage", ticketId, nuevoMensaje.Mensaje, senderName, false);

                    _logger.LogInformation("Mensaje enviado exitosamente para ticket {TicketId}", ticketId);
                    return new JsonResult(new { success = true, message = "Mensaje enviado exitosamente.", ticketId });
                }
                else
                {
                    _logger.LogWarning("No se pudo insertar el mensaje para ticket {TicketId}", ticketId);
                    return new JsonResult(new { success = false, message = "No se pudo enviar el mensaje. Intenta nuevamente." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar mensaje");
                return new JsonResult(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnGetSeleccionarTicketAsync(int ticketId)
        {
            try
            {
                _logger.LogInformation("Iniciando OnGetSeleccionarTicketAsync con ticketId: {TicketId}", ticketId);

                int? userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    _logger.LogWarning("Usuario no autenticado al seleccionar ticket {TicketId}", ticketId);
                    return new JsonResult(new { success = false, message = "Debes iniciar sesión." });
                }

                _logger.LogInformation("Verificando ticket {TicketId} para usuario {UserId}", ticketId, userId);

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string checkQuery = "SELECT COUNT(*) FROM Soporte WHERE id_ticket = @TicketId AND id_usuario = @UserId";
                    using (var command = new SqlCommand(checkQuery, connection))
                    {
                        command.Parameters.AddWithValue("@TicketId", ticketId);
                        command.Parameters.AddWithValue("@UserId", userId.Value);
                        object? result = await command.ExecuteScalarAsync();
                        int count = result != null ? Convert.ToInt32(result) : 0;
                        if (count == 0)
                        {
                            _logger.LogWarning("Ticket {TicketId} no encontrado o no pertenece al usuario {UserId}", ticketId, userId);
                            return new JsonResult(new { success = false, message = "El ticket seleccionado no existe o no tienes acceso." });
                        }
                    }
                }

                PreguntasFrecuentes = new List<PreguntaFrecuenteViewModel>();
                TicketsUsuario = new List<TicketSoporteViewModel>();
                MensajesChat = new List<MensajeChatViewModel>();
                IdTicketSeleccionado = ticketId;

                await CargarPreguntasFrecuentesAsync();
                await CargarTicketsUsuarioAsync(userId.Value);
                await CargarMensajesChatAsync(ticketId);

                _logger.LogInformation("Devolviendo HTML para chatBox con {MessageCount} mensajes", MensajesChat.Count);
                return Partial("_ChatBox", this);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al seleccionar ticket {TicketId}", ticketId);
                return new JsonResult(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        private async Task<string> GetUserNameAsync(int userId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = "SELECT Nombre FROM Usuarios WHERE id_usuario = @UserId";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    var result = await command.ExecuteScalarAsync();
                    return result?.ToString() ?? "Usuario";
                }
            }
        }

        private async Task<int> ObtenerOCrearTicketAsync(int userId, string mensaje)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT TOP 1 id_ticket
                    FROM Soporte
                    WHERE id_usuario = @UserId AND estado != 'cerrado'
                    ORDER BY fecha DESC";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    var result = await command.ExecuteScalarAsync();
                    if (result != null)
                    {
                        return Convert.ToInt32(result);
                    }
                }

                query = @"
                    INSERT INTO Soporte (id_usuario, mensaje, estado, fecha)
                    OUTPUT INSERTED.id_ticket
                    VALUES (@UserId, @Mensaje, 'abierto', @Fecha)";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    command.Parameters.AddWithValue("@Mensaje", mensaje);
                    command.Parameters.AddWithValue("@Fecha", DateTime.Now);
                    return Convert.ToInt32(await command.ExecuteScalarAsync());
                }
            }
        }

        private async Task<bool> EnviarMensajeAsync(int ticketId, int userId, string mensaje)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    INSERT INTO MensajesSoporte (id_ticket, id_usuario, mensaje, fecha, es_admin)
                    VALUES (@TicketId, @UserId, @Mensaje, @Fecha, 0)";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@TicketId", ticketId);
                    command.Parameters.AddWithValue("@UserId", userId);
                    command.Parameters.AddWithValue("@Mensaje", mensaje);
                    command.Parameters.AddWithValue("@Fecha", DateTime.Now);
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        private async Task CargarPreguntasFrecuentesAsync(string terminoBusqueda = "")
        {
            PreguntasFrecuentes.Clear();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = string.IsNullOrWhiteSpace(terminoBusqueda)
                    ? "SELECT TOP 4 id_pregunta, pregunta, respuesta, fecha FROM PreguntasFrecuentes WHERE Habilitado = 1 ORDER BY fecha DESC"
                    : "SELECT id_pregunta, pregunta, respuesta, fecha FROM PreguntasFrecuentes WHERE pregunta LIKE @TerminoBusqueda AND Habilitado = 1 ORDER BY fecha DESC";

                using (var command = new SqlCommand(query, connection))
                {
                    if (!string.IsNullOrWhiteSpace(terminoBusqueda))
                        command.Parameters.AddWithValue("@TerminoBusqueda", $"%{terminoBusqueda}%");

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            PreguntasFrecuentes.Add(new PreguntaFrecuenteViewModel
                            {
                                Id = reader.GetInt32(0),
                                Pregunta = reader.GetString(1),
                                Respuesta = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Fecha = reader.GetDateTime(3)
                            });
                        }
                    }
                }
            }
        }

        private async Task CargarTicketsUsuarioAsync(int userId)
        {
            TicketsUsuario.Clear();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = "SELECT id_ticket, mensaje, estado, fecha FROM Soporte WHERE id_usuario = @UserId ORDER BY fecha DESC";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            TicketsUsuario.Add(new TicketSoporteViewModel
                            {
                                Id = reader.GetInt32(0),
                                Mensaje = reader.GetString(1),
                                Estado = reader.GetString(2),
                                Fecha = reader.GetDateTime(3)
                            });
                        }
                    }
                }
            }
        }

        private async Task CargarMensajesChatAsync(int ticketId)
        {
            MensajesChat.Clear();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"
                    SELECT id_mensaje, id_usuario, mensaje, fecha, es_admin
                    FROM MensajesSoporte
                    WHERE id_ticket = @TicketId
                    ORDER BY fecha ASC";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@TicketId", ticketId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            MensajesChat.Add(new MensajeChatViewModel
                            {
                                IdMensaje = reader.GetInt32(0),
                                IdUsuario = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                                Mensaje = reader.GetString(2),
                                Fecha = reader.GetDateTime(3),
                                EsAdmin = reader.GetBoolean(4)
                            });
                        }
                    }
                }
            }
        }
    }

    public class PreguntaFrecuenteViewModel
    {
        public int Id { get; set; }
        public string Pregunta { get; set; } = string.Empty;
        public string Respuesta { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
    }

    public class TicketSoporteViewModel
    {
        public int Id { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
    }

    public class MensajeChatViewModel
    {
        public int IdMensaje { get; set; }
        public int? IdUsuario { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public bool EsAdmin { get; set; }
    }

    public class NuevoMensajeModel
    {
        public string Mensaje { get; set; } = string.Empty;
    }
}