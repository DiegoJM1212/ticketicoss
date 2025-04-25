using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace ticketicos.Pages.Admin
{
    public class AdminPreventaModel : PageModel
    {
        private readonly ILogger<AdminPreventaModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public AdminPreventaModel(ILogger<AdminPreventaModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        public List<EventoViewModel> Eventos { get; set; } = new List<EventoViewModel>();

        [TempData]
        public string? MensajeExito { get; set; }

        [TempData]
        public string? MensajeError { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la administración de preventas");
                    return RedirectToPage("/Cliente/Login");
                }

                await CargarEventosAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar la página de administración de preventas");
                MensajeError = $"Ocurrió un error al cargar los eventos: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostTogglePreventaAsync(int id, bool estado)
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para modificar preventa");
                    return new JsonResult(new { success = false, message = "Acceso no autorizado." });
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Verificar si el evento existe y está activo
                    string checkEventQuery = @"
                        SELECT fecha FROM Eventos 
                        WHERE id_evento = @IdEvento AND estado = 'activo'";

                    using (SqlCommand checkEventCommand = new SqlCommand(checkEventQuery, connection))
                    {
                        checkEventCommand.Parameters.AddWithValue("@IdEvento", id);
                        var eventDate = await checkEventCommand.ExecuteScalarAsync();

                        if (eventDate == null || eventDate == DBNull.Value)
                        {
                            _logger.LogWarning($"Evento {id} no existe o no está activo");
                            return new JsonResult(new { success = false, message = "El evento no existe o no está activo." });
                        }
                    }

                    // Verificar si existe una preventa
                    string checkQuery = @"
                        SELECT id_preventa, estado FROM Preventa 
                        WHERE id_evento = @IdEvento AND estado != 'cancelada'";

                    using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@IdEvento", id);
                        using (SqlDataReader reader = await checkCommand.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                string currentEstado = reader.GetString(1);
                                if (estado && currentEstado == "activa")
                                {
                                    return new JsonResult(new { success = false, message = "La preventa ya está activa." });
                                }
                                if (!estado && currentEstado == "finalizada")
                                {
                                    return new JsonResult(new { success = false, message = "La preventa ya está finalizada." });
                                }
                            }
                            else if (!estado)
                            {
                                return new JsonResult(new { success = true, message = "No hay preventa activa para desactivar." });
                            }
                        }
                    }

                    if (estado)
                    {
                        // Crear o activar preventa
                        string query = @"
                            IF EXISTS (SELECT 1 FROM Preventa WHERE id_evento = @IdEvento AND estado != 'cancelada')
                                UPDATE Preventa 
                                SET estado = 'activa',
                                    fecha_inicio = COALESCE(fecha_inicio, DATEADD(day, 1, GETDATE())),
                                    fecha_fin = COALESCE(fecha_fin, DATEADD(day, 15, GETDATE())),
                                    cantidad_boletos = COALESCE(cantidad_boletos, 500),
                                    precio_especial = COALESCE(precio_especial, 10000.00),
                                    limite_boletos_por_usuario = COALESCE(limite_boletos_por_usuario, 2)
                                WHERE id_evento = @IdEvento AND estado != 'cancelada'
                            ELSE
                                INSERT INTO Preventa (id_evento, fecha_inicio, fecha_fin, estado, cantidad_boletos, precio_especial, limite_boletos_por_usuario)
                                VALUES (@IdEvento, DATEADD(day, 1, GETDATE()), DATEADD(day, 15, GETDATE()), 'activa', 500, 10000.00, 2)";

                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@IdEvento", id);
                            await command.ExecuteNonQueryAsync();
                            return new JsonResult(new { success = true, message = "Preventa activada exitosamente." });
                        }
                    }
                    else
                    {
                        // Desactivar preventa
                        string query = @"
                            UPDATE Preventa 
                            SET estado = 'finalizada' 
                            WHERE id_evento = @IdEvento AND estado = 'activa'";

                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@IdEvento", id);
                            int rowsAffected = await command.ExecuteNonQueryAsync();
                            return new JsonResult(new { success = true, message = rowsAffected > 0 ? "Preventa desactivada exitosamente." : "No hay preventa activa para desactivar." });
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, $"Error de base de datos al cambiar estado de preventa para evento {id}");
                return new JsonResult(new { success = false, message = $"Error de base de datos: {ex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al cambiar estado de preventa para evento {id}");
                return new JsonResult(new { success = false, message = $"Error al cambiar el estado de la preventa: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostActualizarLimiteAsync(int id, int limite)
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para modificar límite de preventa");
                    return new JsonResult(new { success = false, message = "Acceso no autorizado." });
                }

                if (limite < 1 || limite > 1000)
                {
                    return new JsonResult(new { success = false, message = "El límite de boletos debe estar entre 1 y 1000." });
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Verificar si el evento existe y está activo
                    string checkEventQuery = @"
                        SELECT fecha FROM Eventos 
                        WHERE id_evento = @IdEvento AND estado = 'activo'";

                    using (SqlCommand checkEventCommand = new SqlCommand(checkEventQuery, connection))
                    {
                        checkEventCommand.Parameters.AddWithValue("@IdEvento", id);
                        var eventDate = await checkEventCommand.ExecuteScalarAsync();

                        if (eventDate == null || eventDate == DBNull.Value)
                        {
                            _logger.LogWarning($"Evento {id} no existe o no está activo");
                            return new JsonResult(new { success = false, message = "El evento no existe o no está activo." });
                        }
                    }

                    // Actualizar o crear preventa
                    string query = @"
                        IF EXISTS (SELECT 1 FROM Preventa WHERE id_evento = @IdEvento AND estado != 'cancelada')
                            UPDATE Preventa 
                            SET cantidad_boletos = @Limite,
                                fecha_inicio = COALESCE(fecha_inicio, DATEADD(day, 1, GETDATE())),
                                fecha_fin = COALESCE(fecha_fin, DATEADD(day, 15, GETDATE())),
                                estado = 'activa',
                                precio_especial = COALESCE(precio_especial, 10000.00),
                                limite_boletos_por_usuario = COALESCE(limite_boletos_por_usuario, 2)
                            WHERE id_evento = @IdEvento AND estado != 'cancelada'
                        ELSE
                            INSERT INTO Preventa (id_evento, fecha_inicio, fecha_fin, estado, cantidad_boletos, precio_especial, limite_boletos_por_usuario)
                            VALUES (@IdEvento, DATEADD(day, 1, GETDATE()), DATEADD(day, 15, GETDATE()), 'activa', @Limite, 10000.00, 2)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdEvento", id);
                        command.Parameters.AddWithValue("@Limite", limite);
                        await command.ExecuteNonQueryAsync();
                        return new JsonResult(new { success = true, message = "Límite de boletos actualizado exitosamente." });
                    }
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, $"Error de base de datos al actualizar límite de boletos para evento {id}");
                return new JsonResult(new { success = false, message = $"Error de base de datos: {ex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al actualizar límite de boletos para evento {id}");
                return new JsonResult(new { success = false, message = $"Error al actualizar el límite de boletos: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostActualizarPrecioAsync(int id, decimal precio)
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para modificar precio de preventa");
                    return new JsonResult(new { success = false, message = "Acceso no autorizado." });
                }

                if (precio < 0)
                {
                    return new JsonResult(new { success = false, message = "El precio especial no puede ser negativo." });
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Verificar si el evento existe y está activo
                    string checkEventQuery = @"
                        SELECT fecha FROM Eventos 
                        WHERE id_evento = @IdEvento AND estado = 'activo'";

                    using (SqlCommand checkEventCommand = new SqlCommand(checkEventQuery, connection))
                    {
                        checkEventCommand.Parameters.AddWithValue("@IdEvento", id);
                        var eventDate = await checkEventCommand.ExecuteScalarAsync();

                        if (eventDate == null || eventDate == DBNull.Value)
                        {
                            _logger.LogWarning($"Evento {id} no existe o no está activo");
                            return new JsonResult(new { success = false, message = "El evento no existe o no está activo." });
                        }
                    }

                    // Actualizar o crear preventa
                    string query = @"
                        IF EXISTS (SELECT 1 FROM Preventa WHERE id_evento = @IdEvento AND estado != 'cancelada')
                            UPDATE Preventa 
                            SET precio_especial = @Precio,
                                fecha_inicio = COALESCE(fecha_inicio, DATEADD(day, 1, GETDATE())),
                                fecha_fin = COALESCE(fecha_fin, DATEADD(day, 15, GETDATE())),
                                estado = 'activa',
                                cantidad_boletos = COALESCE(cantidad_boletos, 500),
                                limite_boletos_por_usuario = COALESCE(limite_boletos_por_usuario, 2)
                            WHERE id_evento = @IdEvento AND estado != 'cancelada'
                        ELSE
                            INSERT INTO Preventa (id_evento, fecha_inicio, fecha_fin, estado, cantidad_boletos, precio_especial, limite_boletos_por_usuario)
                            VALUES (@IdEvento, DATEADD(day, 1, GETDATE()), DATEADD(day, 15, GETDATE()), 'activa', 500, @Precio, 2)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdEvento", id);
                        command.Parameters.AddWithValue("@Precio", precio);
                        await command.ExecuteNonQueryAsync();
                        return new JsonResult(new { success = true, message = "Precio especial actualizado exitosamente." });
                    }
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, $"Error de base de datos al actualizar precio especial para evento {id}");
                return new JsonResult(new { success = false, message = $"Error de base de datos: {ex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al actualizar precio especial para evento {id}");
                return new JsonResult(new { success = false, message = $"Error al actualizar el precio especial: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnPostActualizarFechasPreventaAsync(int id, DateTime? fechaInicio, DateTime? fechaFin)
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para modificar fechas de preventa");
                    return new JsonResult(new { success = false, message = "Acceso no autorizado." });
                }

                if (!fechaInicio.HasValue || !fechaFin.HasValue)
                {
                    return new JsonResult(new { success = false, message = "Las fechas de inicio y fin son obligatorias." });
                }

                if (fechaInicio >= fechaFin)
                {
                    return new JsonResult(new { success = false, message = "La fecha de inicio debe ser anterior a la fecha de fin." });
                }

                if (fechaInicio < DateTime.Today)
                {
                    return new JsonResult(new { success = false, message = "La fecha de inicio no puede ser anterior a hoy." });
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Verificar si el evento existe y obtener su fecha
                    string checkEventQuery = @"
                        SELECT fecha FROM Eventos 
                        WHERE id_evento = @IdEvento AND estado = 'activo'";

                    using (SqlCommand checkEventCommand = new SqlCommand(checkEventQuery, connection))
                    {
                        checkEventCommand.Parameters.AddWithValue("@IdEvento", id);
                        var eventDate = await checkEventCommand.ExecuteScalarAsync();

                        if (eventDate == null || eventDate == DBNull.Value)
                        {
                            _logger.LogWarning($"Evento {id} no existe o no está activo");
                            return new JsonResult(new { success = false, message = "El evento no existe o no está activo." });
                        }

                        DateTime fechaEvento = (DateTime)eventDate;
                        if (fechaFin > fechaEvento)
                        {
                            return new JsonResult(new { success = false, message = "La fecha de fin de la preventa no puede ser posterior a la fecha del evento." });
                        }
                    }

                    // Actualizar o crear preventa
                    string query = @"
                        IF EXISTS (SELECT 1 FROM Preventa WHERE id_evento = @IdEvento AND estado != 'cancelada')
                            UPDATE Preventa 
                            SET fecha_inicio = @FechaInicio, 
                                fecha_fin = @FechaFin,
                                estado = 'activa',
                                cantidad_boletos = COALESCE(cantidad_boletos, 500),
                                precio_especial = COALESCE(precio_especial, 10000.00),
                                limite_boletos_por_usuario = COALESCE(limite_boletos_por_usuario, 2)
                            WHERE id_evento = @IdEvento AND estado != 'cancelada'
                        ELSE
                            INSERT INTO Preventa (id_evento, fecha_inicio, fecha_fin, estado, cantidad_boletos, precio_especial, limite_boletos_por_usuario)
                            VALUES (@IdEvento, @FechaInicio, @FechaFin, 'activa', 500, 10000.00, 2)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdEvento", id);
                        command.Parameters.AddWithValue("@FechaInicio", fechaInicio.Value);
                        command.Parameters.AddWithValue("@FechaFin", fechaFin.Value);
                        await command.ExecuteNonQueryAsync();
                        return new JsonResult(new { success = true, message = "Fechas de preventa actualizadas exitosamente." });
                    }
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, $"Error de base de datos al actualizar fechas de preventa para evento {id}");
                return new JsonResult(new { success = false, message = $"Error de base de datos: {ex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al actualizar fechas de preventa para evento {id}");
                return new JsonResult(new { success = false, message = $"Error al actualizar las fechas de preventa: {ex.Message}" });
            }
        }

        private async Task CargarEventosAsync()
        {
            Eventos.Clear();

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT e.id_evento, e.nombre, e.fecha, e.lugar, e.imagen_url, 
                               p.id_preventa, p.estado AS estado_preventa, p.fecha_inicio, p.fecha_fin, 
                               ISNULL(p.cantidad_boletos, 500) AS cantidad_boletos,
                               ISNULL(p.precio_especial, 10000.00) AS precio_especial,
                               ISNULL(p.limite_boletos_por_usuario, 2) AS limite_boletos_por_usuario
                        FROM Eventos e
                        LEFT JOIN Preventa p ON e.id_evento = p.id_evento AND p.estado != 'cancelada'
                        WHERE e.estado = 'activo'
                        ORDER BY e.fecha ASC";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int id = reader.GetInt32(0);
                                string nombre = reader.GetString(1);
                                DateTime fecha = reader.GetDateTime(2);
                                string lugar = reader.GetString(3);

                                string imagenUrl = "/images/default-event.jpg";
                                if (!reader.IsDBNull(4))
                                {
                                    string imgUrl = reader.GetString(4);
                                    if (imgUrl.StartsWith("http"))
                                    {
                                        imagenUrl = imgUrl;
                                    }
                                    else
                                    {
                                        imagenUrl = $"/Content/{imgUrl.TrimStart('/')}";
                                    }
                                }

                                bool enPreventa = false;
                                DateTime? fechaInicioPreventa = null;
                                DateTime? fechaFinPreventa = null;
                                int cantidadBoletos = 500;
                                decimal precioEspecial = 10000.00m;
                                int limiteBoletosPorUsuario = 2;

                                if (!reader.IsDBNull(5))
                                {
                                    string estadoPreventa = reader.GetString(6);
                                    enPreventa = estadoPreventa == "activa";

                                    if (!reader.IsDBNull(7))
                                        fechaInicioPreventa = reader.GetDateTime(7);

                                    if (!reader.IsDBNull(8))
                                        fechaFinPreventa = reader.GetDateTime(8);

                                    cantidadBoletos = reader.GetInt32(9);
                                    precioEspecial = reader.GetDecimal(10);
                                    limiteBoletosPorUsuario = reader.GetInt32(11);
                                }

                                var evento = new EventoViewModel
                                {
                                    Id = id,
                                    Nombre = nombre,
                                    Fecha = fecha,
                                    Hora = fecha.ToString("HH:mm"),
                                    Lugar = lugar,
                                    ImagenUrl = imagenUrl,
                                    EnPreventa = enPreventa,
                                    FechaInicioPreventa = fechaInicioPreventa,
                                    FechaFinPreventa = fechaFinPreventa,
                                    CantidadBoletos = cantidadBoletos,
                                    PrecioEspecial = precioEspecial,
                                    LimiteBoletosPorUsuario = limiteBoletosPorUsuario
                                };

                                Eventos.Add(evento);
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Error de base de datos al cargar eventos");
                throw new Exception($"Error de base de datos al cargar eventos: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar eventos");
                throw new Exception($"Error al cargar eventos: {ex.Message}", ex);
            }
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
        public bool EnPreventa { get; set; }
        public DateTime? FechaInicioPreventa { get; set; }
        public DateTime? FechaFinPreventa { get; set; }
        public int CantidadBoletos { get; set; } = 500;
        public decimal PrecioEspecial { get; set; } = 10000.00m;
        public int LimiteBoletosPorUsuario { get; set; } = 2;
    }
}