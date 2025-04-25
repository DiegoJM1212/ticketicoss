using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ticketicos.Services;
using DinkToPdf;
using DinkToPdf.Contracts;

namespace ticketicos.Pages.Cliente
{
    public class FacturaModel : PageModel
    {
        private readonly ILogger<FacturaModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;
        private readonly IConverter _pdfConverter;
        private readonly string _connectionString;

        public FacturaModel(ILogger<FacturaModel> logger, IConfiguration configuration, EmailService emailService, IConverter pdfConverter)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _pdfConverter = pdfConverter ?? throw new ArgumentNullException(nameof(pdfConverter));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        [BindProperty(SupportsGet = true)]
        public int? FacturaId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string TipoCompra { get; set; } = string.Empty;

        public string NumeroFactura { get; set; } = string.Empty;
        public string Evento { get; set; } = string.Empty;
        public string FechaHora { get; set; } = string.Empty;
        public string Lugar { get; set; } = string.Empty;
        public int CantidadBoletos { get; set; }
        public string Total { get; set; } = string.Empty;
        public string CorreoElectronico { get; set; } = string.Empty;
        public string CodigoPromo { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            if (!FacturaId.HasValue)
            {
                _logger.LogWarning("FacturaId no proporcionado, redirigiendo a la página principal");
                return Redirect("/");
            }

            try
            {
                // Determinar el tipo de compra si no se especificó
                if (string.IsNullOrEmpty(TipoCompra))
                {
                    TipoCompra = await DeterminarTipoCompraAsync(FacturaId.Value);
                }

                // Cargar los datos de la factura según el tipo de compra
                if (TipoCompra.Equals("entradas", StringComparison.OrdinalIgnoreCase))
                {
                    await CargarDatosFacturaEntradasAsync(FacturaId.Value);
                }
                else if (TipoCompra.Equals("codigos", StringComparison.OrdinalIgnoreCase))
                {
                    await CargarDatosFacturaCodigosAsync(FacturaId.Value);
                }
                else
                {
                    _logger.LogWarning($"Tipo de compra desconocido: {TipoCompra} para FacturaId: {FacturaId}");
                    return Redirect("/");
                }

                // Enviar la factura por correo electrónico
                await EnviarFacturaPorCorreoAsync();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al cargar la factura {FacturaId}: {ex.Message}");
                return Redirect("/");
            }
        }

        public async Task<IActionResult> OnGetDescargarPDFCodigoAsync(int id)
        {
            if (id != FacturaId || !TipoCompra.Equals("codigos", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning($"Solicitud de PDF inválida: FacturaId: {id}, TipoCompra: {TipoCompra}");
                return BadRequest();
            }

            try
            {
                await CargarDatosFacturaCodigosAsync(id);
                string htmlContent = GenerarHtmlFactura();
                byte[] pdfBytes = GeneratePdf(htmlContent);
                _logger.LogInformation($"PDF generado para código promocional, FacturaId: {id}");
                return File(pdfBytes, "application/pdf", $"Factura_Codigo_{NumeroFactura}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al generar PDF para código promocional, FacturaId: {id}: {ex.Message}");
                return StatusCode(500);
            }
        }

        public async Task<IActionResult> OnGetDescargarPDFEntradasAsync(int id)
        {
            if (id != FacturaId || !TipoCompra.Equals("entradas", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning($"Solicitud de PDF inválida: FacturaId: {id}, TipoCompra: {TipoCompra}");
                return BadRequest();
            }

            try
            {
                await CargarDatosFacturaEntradasAsync(id);
                string htmlContent = GenerarHtmlBoletos();
                byte[] pdfBytes = GeneratePdf(htmlContent);
                _logger.LogInformation($"PDF generado para entradas, FacturaId: {id}");
                return File(pdfBytes, "application/pdf", $"Entradas_{NumeroFactura}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al generar PDF para entradas, FacturaId: {id}: {ex.Message}");
                return StatusCode(500);
            }
        }

        private async Task<string> DeterminarTipoCompraAsync(int facturaId)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Verificar si es una compra de entradas
                string queryEntradas = @"
                    SELECT COUNT(1) FROM Compras WHERE id_compra = @FacturaId";

                using (SqlCommand command = new SqlCommand(queryEntradas, connection))
                {
                    command.Parameters.AddWithValue("@FacturaId", facturaId);
                    var result = await command.ExecuteScalarAsync();
                    int count = result != null ? Convert.ToInt32(result) : 0;

                    if (count > 0)
                    {
                        return "entradas";
                    }
                }

                // Verificar si es de códigos promocionales
                string queryCodigos = @"
                    SELECT COUNT(1) FROM ComprasCodigos WHERE id_compra = @FacturaId";

                using (SqlCommand command = new SqlCommand(queryCodigos, connection))
                {
                    command.Parameters.AddWithValue("@FacturaId", facturaId);
                    var result = await command.ExecuteScalarAsync();
                    int count = result != null ? Convert.ToInt32(result) : 0;

                    if (count > 0)
                    {
                        return "codigos";
                    }
                }

                _logger.LogWarning($"No se encontró compra para FacturaId: {facturaId}");
                return "desconocido";
            }
        }

        private async Task CargarDatosFacturaEntradasAsync(int facturaId)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT c.id_compra, c.fecha, c.total, c.numero_asiento, c.correo_electronico,
                           e.nombre AS evento_nombre, e.fecha AS evento_fecha, e.lugar, e.hora,
                           cp.codigo AS codigo_promo
                    FROM Compras c
                    INNER JOIN Eventos e ON c.id_evento = e.id_evento
                    LEFT JOIN CodigosPromocionales cp ON c.id_codigo_promocional = cp.id_codigo
                    WHERE c.id_compra = @FacturaId";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@FacturaId", facturaId);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            NumeroFactura = $"E-{facturaId:D6}";
                            Evento = reader.GetString(reader.GetOrdinal("evento_nombre"));

                            DateTime fechaEvento = reader.GetDateTime(reader.GetOrdinal("evento_fecha"));
                            string horaEvento = reader.IsDBNull(reader.GetOrdinal("hora")) ? "N/A" : reader.GetString(reader.GetOrdinal("hora"));
                            FechaHora = $"{fechaEvento:dd/MM/yyyy} {horaEvento}";

                            Lugar = reader.GetString(reader.GetOrdinal("lugar"));
                            string numeroAsiento = reader.IsDBNull(reader.GetOrdinal("numero_asiento")) ? "" : reader.GetString(reader.GetOrdinal("numero_asiento"));
                            CantidadBoletos = string.IsNullOrEmpty(numeroAsiento) ? 1 : numeroAsiento.Split(',').Length;
                            Total = $"₡{reader.GetDecimal(reader.GetOrdinal("total")):N0}";
                            CorreoElectronico = reader.IsDBNull(reader.GetOrdinal("correo_electronico")) ? "N/A" : reader.GetString(reader.GetOrdinal("correo_electronico"));
                            CodigoPromo = reader.IsDBNull(reader.GetOrdinal("codigo_promo")) ? "" : reader.GetString(reader.GetOrdinal("codigo_promo"));
                        }
                        else
                        {
                            _logger.LogWarning($"No se encontró factura de entradas con ID: {facturaId}");
                            throw new Exception("Factura no encontrada");
                        }
                    }
                }
            }
        }

        private async Task CargarDatosFacturaCodigosAsync(int facturaId)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT cc.id_compra, cc.fecha, cc.total, cc.correo_electronico, cc.codigo_generado,
                           cp.descripcion, cp.descuento
                    FROM ComprasCodigos cc
                    INNER JOIN CodigosPromocionales cp ON cc.id_codigo = cp.id_codigo
                    WHERE cc.id_compra = @FacturaId";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@FacturaId", facturaId);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            NumeroFactura = $"C-{facturaId:D6}";
                            Evento = "Código Promocional";

                            string descripcion = reader.IsDBNull(reader.GetOrdinal("descripcion"))
                                ? "Código Promocional"
                                : reader.GetString(reader.GetOrdinal("descripcion"));

                            FechaHora = $"{reader.GetDateTime(reader.GetOrdinal("fecha")):dd/MM/yyyy HH:mm}";
                            Lugar = "N/A";
                            CantidadBoletos = 1;
                            Total = $"₡{reader.GetDecimal(reader.GetOrdinal("total")):N0}";
                            CorreoElectronico = reader.IsDBNull(reader.GetOrdinal("correo_electronico")) ? "N/A" : reader.GetString(reader.GetOrdinal("correo_electronico"));
                            CodigoPromo = reader.GetString(reader.GetOrdinal("codigo_generado"));
                        }
                        else
                        {
                            _logger.LogWarning($"No se encontró factura de códigos con ID: {facturaId}");
                            throw new Exception("Factura no encontrada");
                        }
                    }
                }
            }
        }

        private async Task EnviarFacturaPorCorreoAsync()
        {
            try
            {
                string facturaHtml = GenerarHtmlFactura();
                await _emailService.SendInvoiceAsync(CorreoElectronico, NumeroFactura, facturaHtml);

                if (TipoCompra.Equals("entradas", StringComparison.OrdinalIgnoreCase))
                {
                    string ticketsHtml = GenerarHtmlBoletos();
                    await _emailService.SendTicketsAsync(CorreoElectronico, Evento, ticketsHtml);
                }
                else if (TipoCompra.Equals("codigos", StringComparison.OrdinalIgnoreCase))
                {
                    await _emailService.SendPromoCodeAsync(CorreoElectronico, CodigoPromo);
                }

                _logger.LogInformation($"Factura {NumeroFactura} enviada a {CorreoElectronico}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al enviar la factura {NumeroFactura} por correo a {CorreoElectronico}: {ex.Message}");
            }
        }

        private string GenerarHtmlFactura()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <title>Factura - Tickicos</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: Arial, sans-serif; }");
            sb.AppendLine("        .container { max-width: 800px; margin: 0 auto; padding: 20px; }");
            sb.AppendLine("        .header { text-align: center; margin-bottom: 30px; }");
            sb.AppendLine("        .logo { background-color: #c1ff00; width: 70px; height: 70px; margin: 0 auto; display: flex; align-items: center; justify-content: center; border-radius: 10px; }");
            sb.AppendLine("        .invoice-title { font-size: 24px; margin: 20px 0; }");
            sb.AppendLine("        .invoice-details { margin-bottom: 30px; }");
            sb.AppendLine("        .invoice-details table { width: 100%; border-collapse: collapse; }");
            sb.AppendLine("        .invoice-details th, .invoice-details td { padding: 10px; text-align: left; border-bottom: 1px solid #ddd; }");
            sb.AppendLine("        .total { font-size: 18px; font-weight: bold; text-align: right; margin-top: 20px; }");
            sb.AppendLine("        .footer { margin-top: 50px; text-align: center; font-size: 12px; color: #666; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <div class=\"container\">");
            sb.AppendLine("        <div class=\"header\">");
            sb.AppendLine("            <div class=\"logo\">TICKICOS</div>");
            sb.AppendLine("            <h1 class=\"invoice-title\">Factura</h1>");
            sb.AppendLine("        </div>");

            sb.AppendLine("        <div class=\"invoice-details\">");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th>Número de Factura:</th>");
            sb.AppendLine($"                    <td>{NumeroFactura}</td>");
            sb.AppendLine("                </tr>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th>Fecha:</th>");
            sb.AppendLine($"                    <td>{DateTime.Now:dd/MM/yyyy HH:mm}</td>");
            sb.AppendLine("                </tr>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th>Cliente:</th>");
            sb.AppendLine($"                    <td>{CorreoElectronico}</td>");
            sb.AppendLine("                </tr>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");

            sb.AppendLine("        <div class=\"invoice-items\">");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th>Descripción</th>");
            sb.AppendLine("                    <th>Cantidad</th>");
            sb.AppendLine("                    <th>Precio</th>");
            sb.AppendLine("                </tr>");

            if (TipoCompra.Equals("entradas", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("                <tr>");
                sb.AppendLine($"                    <td>Entradas para {Evento} - {FechaHora} - {Lugar}{(string.IsNullOrEmpty(CodigoPromo) ? "" : $"<br>Código Promocional: {CodigoPromo}")}</td>");
                sb.AppendLine($"                    <td>{CantidadBoletos}</td>");
                sb.AppendLine($"                    <td>{Total}</td>");
                sb.AppendLine("                </tr>");
            }
            else if (TipoCompra.Equals("codigos", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("                <tr>");
                sb.AppendLine($"                    <td>Código Promocional - {Evento} ({CodigoPromo})</td>");
                sb.AppendLine($"                    <td>{CantidadBoletos}</td>");
                sb.AppendLine($"                    <td>{Total}</td>");
                sb.AppendLine("                </tr>");
            }

            sb.AppendLine("            </table>");

            sb.AppendLine("            <div class=\"total\">");
            sb.AppendLine($"                Total: {Total}");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");

            sb.AppendLine("        <div class=\"footer\">");
            sb.AppendLine("            <p>Gracias por tu compra. Para cualquier consulta, contacta con nuestro servicio de atención al cliente.</p>");
            sb.AppendLine("            <p>© 2025 Tickicos - Todos los derechos reservados</p>");
            sb.AppendLine("        </div>");
            sb.AppendLine("    </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private string GenerarHtmlBoletos()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <title>Tus Entradas - Tickicos</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: Arial, sans-serif; }");
            sb.AppendLine("        .container { max-width: 800px; margin: 0 auto; padding: 20px; }");
            sb.AppendLine("        .header { text-align: center; margin-bottom: 30px; }");
            sb.AppendLine("        .logo { background-color: #c1ff00; width: 70px; height: 70px; margin: 0 auto; display: flex; align-items: center; justify-content: center; border-radius: 10px; }");
            sb.AppendLine("        .ticket-title { font-size: 24px; margin: 20px 0; }");
            sb.AppendLine("        .ticket { border: 2px dashed #333; padding: 20px; margin-bottom: 20px; page-break-inside: avoid; }");
            sb.AppendLine("        .ticket-header { border-bottom: 1px solid #ddd; padding-bottom: 10px; margin-bottom: 10px; }");
            sb.AppendLine("        .ticket-event { font-size: 18px; font-weight: bold; }");
            sb.AppendLine("        .ticket-details { display: flex; justify-content: space-between; }");
            sb.AppendLine("        .ticket-info { flex: 1; }");
            sb.AppendLine("        .ticket-qr { width: 100px; height: 100px; }");
            sb.AppendLine("        .footer { margin-top: 50px; text-align: center; font-size: 12px; color: #666; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <div class=\"container\">");
            sb.AppendLine("        <div class=\"header\">");
            sb.AppendLine("            <div class=\"logo\">TICKICOS</div>");
            sb.AppendLine("            <h1 class=\"ticket-title\">Tus Entradas</h1>");
            sb.AppendLine("        </div>");

            for (int i = 1; i <= CantidadBoletos; i++)
            {
                string ticketId = $"{NumeroFactura}-{i:D2}";
                string qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?data={Uri.EscapeDataString(ticketId)}&size=100x100";

                sb.AppendLine("        <div class=\"ticket\">");
                sb.AppendLine("            <div class=\"ticket-header\">");
                sb.AppendLine($"                <div class=\"ticket-event\">{Evento}</div>");
                sb.AppendLine($"                <div>Boleto #{i} de {CantidadBoletos}</div>");
                sb.AppendLine("            </div>");
                sb.AppendLine("            <div class=\"ticket-details\">");
                sb.AppendLine("                <div class=\"ticket-info\">");
                sb.AppendLine($"                    <p><strong>Fecha y hora:</strong> {FechaHora}</p>");
                sb.AppendLine($"                    <p><strong>Lugar:</strong> {Lugar}</p>");
                sb.AppendLine($"                    <p><strong>ID de Boleto:</strong> {ticketId}</p>");
                if (!string.IsNullOrEmpty(CodigoPromo))
                {
                    sb.AppendLine($"                    <p><strong>Código Promocional:</strong> {CodigoPromo}</p>");
                }
                sb.AppendLine("                </div>");
                sb.AppendLine("                <div class=\"ticket-qr\">");
                sb.AppendLine($"                    <img src=\"{qrUrl}\" alt=\"QR Code\" />");
                sb.AppendLine("                </div>");
                sb.AppendLine("            </div>");
                sb.AppendLine("        </div>");
            }

            sb.AppendLine("        <div class=\"footer\">");
            sb.AppendLine("            <p>Presenta este boleto impreso o en tu dispositivo móvil para acceder al evento.</p>");
            sb.AppendLine("            <p>© 2025 Tickicos - Todos los derechos reservados</p>");
            sb.AppendLine("        </div>");
            sb.AppendLine("    </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private byte[] GeneratePdf(string htmlContent)
        {
            var globalSettings = new GlobalSettings
            {
                ColorMode = ColorMode.Color,
                Orientation = Orientation.Portrait,
                PaperSize = PaperKind.A4,
                Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 },
                DocumentTitle = TipoCompra.Equals("codigos", StringComparison.OrdinalIgnoreCase) ? "Factura Código Promocional" : "Entradas"
            };

            var objectSettings = new ObjectSettings
            {
                PagesCount = true,
                HtmlContent = htmlContent,
                WebSettings = { DefaultEncoding = "utf-8", UserStyleSheet = null },
                HeaderSettings = { FontName = "Arial", FontSize = 9, Right = "Página [page] de [toPage]", Line = true },
                FooterSettings = { FontName = "Arial", FontSize = 9, Line = true, Center = "Tickicos" }
            };

            var pdf = new HtmlToPdfDocument()
            {
                GlobalSettings = globalSettings,
                Objects = { objectSettings }
            };

            return _pdfConverter.Convert(pdf);
        }
    }
}
