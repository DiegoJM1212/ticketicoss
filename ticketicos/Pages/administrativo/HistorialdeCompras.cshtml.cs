using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Text;
using ticketicos.Services;

namespace ticketicos.Pages.Admin
{
    public class HistorialComprasUsuarioModel : PageModel
    {
        private readonly ILogger<HistorialComprasUsuarioModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly EmailService _emailService;

        public HistorialComprasUsuarioModel(ILogger<HistorialComprasUsuarioModel> logger, IConfiguration configuration, EmailService emailService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

        [BindProperty(SupportsGet = true)]
        public int IdUsuario { get; set; }

        public string NombreUsuario { get; set; } = string.Empty;
        public string CorreoUsuario { get; set; } = string.Empty;
        public List<CompraViewModel> Compras { get; set; } = new List<CompraViewModel>();

        [TempData]
        public string? MensajeExito { get; set; }

        [TempData]
        public string? MensajeError { get; set; }

        public async Task<IActionResult> OnGetAsync(int idUsuario)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado al historial de compras");
                    return RedirectToPage("/Cliente/Login");
                }

                IdUsuario = idUsuario;

                // Cargar datos del usuario
                bool usuarioEncontrado = await CargarDatosUsuarioAsync();

                if (!usuarioEncontrado)
                {
                    MensajeError = "Usuario no encontrado.";
                    return RedirectToPage("/Admin/GestionClientes");
                }

                // Cargar historial de compras
                await CargarHistorialComprasAsync();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar el historial de compras del usuario");
                MensajeError = "Ocurrió un error al cargar el historial de compras.";
                return RedirectToPage("/Admin/GestionClientes");
            }
        }

        public async Task<IActionResult> OnPostReenviarBoletoAsync(int idCompra, int idDetalle)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para reenviar boleto");
                    return RedirectToPage("/Cliente/Login");
                }

                // Obtener información del boleto y el evento
                string correoUsuario = string.Empty;
                string nombreEvento = string.Empty;
                DateTime fechaEvento = DateTime.Now;
                string lugarEvento = string.Empty;
                string categoria = string.Empty;
                int cantidad = 0;

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Obtener correo del usuario
                    string queryUsuario = "SELECT correo FROM Usuarios WHERE id_usuario = @IdUsuario";
                    using (SqlCommand command = new SqlCommand(queryUsuario, connection))
                    {
                        command.Parameters.AddWithValue("@IdUsuario", IdUsuario);
                        object result = await command.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            correoUsuario = result.ToString() ?? string.Empty;
                        }
                    }

                    // Obtener información del boleto
                    string queryBoleto = @"
                        SELECT e.nombre, e.fecha, e.lugar, b.categoria, dc.cantidad
                        FROM DetalleCompra dc
                        INNER JOIN Boletos b ON dc.id_boleto = b.id_boleto
                        INNER JOIN Eventos e ON b.id_evento = e.id_evento
                        WHERE dc.id_detalle = @IdDetalle AND dc.id_compra = @IdCompra";

                    using (SqlCommand command = new SqlCommand(queryBoleto, connection))
                    {
                        command.Parameters.AddWithValue("@IdDetalle", idDetalle);
                        command.Parameters.AddWithValue("@IdCompra", idCompra);

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                nombreEvento = reader.GetString(0);
                                fechaEvento = reader.GetDateTime(1);
                                lugarEvento = reader.GetString(2);
                                categoria = reader.GetString(3);
                                cantidad = reader.GetInt32(4);
                            }
                            else
                            {
                                MensajeError = "No se encontró información del boleto.";
                                return RedirectToPage(new { idUsuario = IdUsuario });
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(correoUsuario))
                {
                    MensajeError = "No se pudo obtener el correo del usuario.";
                    return RedirectToPage(new { idUsuario = IdUsuario });
                }

                // Generar HTML para los boletos
                string ticketsHtml = GenerarHtmlBoletos(nombreEvento, fechaEvento, lugarEvento, categoria, cantidad, idCompra, idDetalle);

                // Enviar boletos por correo
                bool enviado = await _emailService.SendTicketsAsync(correoUsuario, nombreEvento, ticketsHtml);

                if (enviado)
                {
                    // Registrar la acción de reenvío
                    using (SqlConnection connection = new SqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();
                        string query = @"
                            INSERT INTO RegistroAcciones (id_usuario_admin, id_usuario_afectado, tipo_accion, 
                                                         detalle_accion, fecha_accion)
                            VALUES (@IdAdmin, @IdUsuario, 'reenvio_boleto', 
                                    @DetalleAccion, 
                                    GETDATE())";

                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@IdAdmin", userId.Value);
                            command.Parameters.AddWithValue("@IdUsuario", IdUsuario);
                            command.Parameters.AddWithValue("@DetalleAccion",
                                $"Reenvío de boleto: Compra #{idCompra}, Detalle #{idDetalle}");
                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    MensajeExito = $"Boletos reenviados exitosamente al correo {correoUsuario}.";
                }
                else
                {
                    MensajeError = "No se pudieron reenviar los boletos. Por favor, intente nuevamente.";
                }

                return RedirectToPage(new { idUsuario = IdUsuario });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reenviar boleto");
                MensajeError = "Ocurrió un error al reenviar el boleto.";
                return RedirectToPage(new { idUsuario = IdUsuario });
            }
        }

        public async Task<IActionResult> OnPostCambiarEstadoCompraAsync(int idCompra, string nuevoEstado)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para cambiar estado de compra");
                    return RedirectToPage("/Cliente/Login");
                }

                // Validar que el nuevo estado sea válido
                if (nuevoEstado != "pendiente" && nuevoEstado != "pagado" && nuevoEstado != "cancelado")
                {
                    MensajeError = "Estado no válido.";
                    return RedirectToPage(new { idUsuario = IdUsuario });
                }

                // Actualizar el estado de la compra
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "UPDATE Compras SET estado = @Estado WHERE id_compra = @IdCompra AND id_usuario = @IdUsuario";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Estado", nuevoEstado);
                        command.Parameters.AddWithValue("@IdCompra", idCompra);
                        command.Parameters.AddWithValue("@IdUsuario", IdUsuario);
                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected == 0)
                        {
                            MensajeError = "No se encontró la compra o no pertenece al usuario.";
                            return RedirectToPage(new { idUsuario = IdUsuario });
                        }
                    }

                    // Registrar la acción
                    string logQuery = @"
                        INSERT INTO RegistroAcciones (id_usuario_admin, id_usuario_afectado, tipo_accion, 
                                                     detalle_accion, fecha_accion)
                        VALUES (@IdAdmin, @IdUsuario, 'cambio_estado_compra', 
                                @DetalleAccion, 
                                GETDATE())";

                    using (SqlCommand logCommand = new SqlCommand(logQuery, connection))
                    {
                        logCommand.Parameters.AddWithValue("@IdAdmin", userId.Value);
                        logCommand.Parameters.AddWithValue("@IdUsuario", IdUsuario);
                        logCommand.Parameters.AddWithValue("@DetalleAccion",
                            $"Cambio de estado de compra #{idCompra} a {nuevoEstado}");
                        await logCommand.ExecuteNonQueryAsync();
                    }
                }

                MensajeExito = $"Estado de la compra actualizado a '{nuevoEstado}'.";
                return RedirectToPage(new { idUsuario = IdUsuario });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado de compra");
                MensajeError = "Ocurrió un error al cambiar el estado de la compra.";
                return RedirectToPage(new { idUsuario = IdUsuario });
            }
        }

        private async Task<bool> CargarDatosUsuarioAsync()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = "SELECT nombre, apellidos, correo FROM Usuarios WHERE id_usuario = @IdUsuario";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@IdUsuario", IdUsuario);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string nombre = reader.GetString(0);
                            string apellidos = reader.GetString(1);
                            CorreoUsuario = reader.GetString(2);
                            NombreUsuario = $"{nombre} {apellidos}";
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private async Task CargarHistorialComprasAsync()
        {
            Compras.Clear();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Consulta para obtener las compras del usuario
                string query = @"
                    SELECT c.id_compra, c.total, c.estado, c.fecha, c.numero_factura, 
                           c.descuento_aplicado, cp.codigo as codigo_promocional
                    FROM Compras c
                    LEFT JOIN CodigosPromocionales cp ON c.id_codigo_promocional = cp.id_codigo
                    WHERE c.id_usuario = @IdUsuario
                    ORDER BY c.fecha DESC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@IdUsuario", IdUsuario);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int idCompra = reader.GetInt32(0);
                            decimal total = reader.GetDecimal(1);
                            string estado = reader.GetString(2);
                            DateTime fecha = reader.GetDateTime(3);
                            string numeroFactura = !reader.IsDBNull(4) ? reader.GetString(4) : string.Empty;
                            decimal descuentoAplicado = !reader.IsDBNull(5) ? reader.GetDecimal(5) : 0;
                            string codigoPromocional = !reader.IsDBNull(6) ? reader.GetString(6) : string.Empty;

                            var compra = new CompraViewModel
                            {
                                IdCompra = idCompra,
                                Total = total,
                                Estado = estado,
                                Fecha = fecha,
                                NumeroFactura = numeroFactura,
                                DescuentoAplicado = descuentoAplicado,
                                CodigoPromocional = codigoPromocional,
                                Detalles = new List<DetalleCompraViewModel>()
                            };

                            Compras.Add(compra);
                        }
                    }
                }

                // Para cada compra, obtener sus detalles
                foreach (var compra in Compras)
                {
                    string detallesQuery = @"
                        SELECT dc.id_detalle, dc.cantidad, dc.subtotal, 
                               e.nombre as nombre_evento, e.fecha as fecha_evento, e.lugar as lugar_evento,
                               b.categoria, b.precio
                        FROM DetalleCompra dc
                        INNER JOIN Boletos b ON dc.id_boleto = b.id_boleto
                        INNER JOIN Eventos e ON b.id_evento = e.id_evento
                        WHERE dc.id_compra = @IdCompra";

                    using (SqlCommand detallesCommand = new SqlCommand(detallesQuery, connection))
                    {
                        detallesCommand.Parameters.AddWithValue("@IdCompra", compra.IdCompra);

                        using (SqlDataReader detallesReader = await detallesCommand.ExecuteReaderAsync())
                        {
                            while (await detallesReader.ReadAsync())
                            {
                                int idDetalle = detallesReader.GetInt32(0);
                                int cantidad = detallesReader.GetInt32(1);
                                decimal subtotal = detallesReader.GetDecimal(2);
                                string nombreEvento = detallesReader.GetString(3);
                                DateTime fechaEvento = detallesReader.GetDateTime(4);
                                string lugarEvento = detallesReader.GetString(5);
                                string categoria = detallesReader.GetString(6);
                                decimal precio = detallesReader.GetDecimal(7);

                                var detalle = new DetalleCompraViewModel
                                {
                                    IdDetalle = idDetalle,
                                    Cantidad = cantidad,
                                    Subtotal = subtotal,
                                    NombreEvento = nombreEvento,
                                    FechaEvento = fechaEvento,
                                    LugarEvento = lugarEvento,
                                    Categoria = categoria,
                                    Precio = precio
                                };

                                compra.Detalles.Add(detalle);
                            }
                        }
                    }
                }
            }
        }

        private string GenerarHtmlBoletos(string nombreEvento, DateTime fechaEvento, string lugarEvento, string categoria, int cantidad, int idCompra, int idDetalle)
        {
            StringBuilder html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <title>Boletos para " + nombreEvento + "</title>");
            html.AppendLine("    <style>");
            html.AppendLine("        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; }");
            html.AppendLine("        .ticket { border: 2px solid #000; margin-bottom: 20px; padding: 15px; page-break-inside: avoid; }");
            html.AppendLine("        .ticket-header { background-color: #C1FF00; color: #000; padding: 10px; text-align: center; font-weight: bold; }");
            html.AppendLine("        .ticket-content { padding: 15px; }");
            html.AppendLine("        .ticket-info { margin-bottom: 10px; }");
            html.AppendLine("        .ticket-footer { border-top: 1px dashed #ccc; padding-top: 10px; text-align: center; font-size: 12px; }");
            html.AppendLine("        .qr-code { text-align: center; margin: 15px 0; }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            html.AppendLine("    <h1>Tus boletos para " + nombreEvento + "</h1>");
            html.AppendLine("    <p>Gracias por tu compra. Aquí están tus boletos:</p>");

            // Generar un boleto por cada cantidad
            for (int i = 0; i < cantidad; i++)
            {
                string codigoBoleto = $"{idCompra}-{idDetalle}-{i + 1}";
                string qrCodeUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=150x150&data={codigoBoleto}";

                html.AppendLine("    <div class=\"ticket\">");
                html.AppendLine("        <div class=\"ticket-header\">");
                html.AppendLine("            " + nombreEvento);
                html.AppendLine("        </div>");
                html.AppendLine("        <div class=\"ticket-content\">");
                html.AppendLine("            <div class=\"ticket-info\"><strong>Fecha:</strong> " + fechaEvento.ToString("dd/MM/yyyy HH:mm") + "</div>");
                html.AppendLine("            <div class=\"ticket-info\"><strong>Lugar:</strong> " + lugarEvento + "</div>");
                html.AppendLine("            <div class=\"ticket-info\"><strong>Categoría:</strong> " + categoria + "</div>");
                html.AppendLine("            <div class=\"ticket-info\"><strong>Boleto:</strong> " + (i + 1) + " de " + cantidad + "</div>");
                html.AppendLine("            <div class=\"ticket-info\"><strong>Código:</strong> " + codigoBoleto + "</div>");
                html.AppendLine("            <div class=\"qr-code\">");
                html.AppendLine("                <img src=\"" + qrCodeUrl + "\" alt=\"Código QR\">");
                html.AppendLine("            </div>");
                html.AppendLine("        </div>");
                html.AppendLine("        <div class=\"ticket-footer\">");
                html.AppendLine("            Este boleto es válido solo para la fecha y hora indicadas. Presenta este boleto impreso o en tu dispositivo móvil.");
                html.AppendLine("        </div>");
                html.AppendLine("    </div>");
            }

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }
    }

    public class CompraViewModel
    {
        public int IdCompra { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string NumeroFactura { get; set; } = string.Empty;
        public decimal DescuentoAplicado { get; set; }
        public string CodigoPromocional { get; set; } = string.Empty;
        public List<DetalleCompraViewModel> Detalles { get; set; } = new List<DetalleCompraViewModel>();
    }

    public class DetalleCompraViewModel
    {
        public int IdDetalle { get; set; }
        public int Cantidad { get; set; }
        public decimal Subtotal { get; set; }
        public string NombreEvento { get; set; } = string.Empty;
        public DateTime FechaEvento { get; set; }
        public string LugarEvento { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal Precio { get; set; }
    }
}

