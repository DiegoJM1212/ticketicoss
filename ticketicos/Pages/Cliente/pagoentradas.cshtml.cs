using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace ticketicos.Pages.Cliente
{
    public class PagoEntradasModel : PageModel
    {
        private readonly ILogger<PagoEntradasModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public PagoEntradasModel(ILogger<PagoEntradasModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");

            DatosPago = new DatosPagoEntradasViewModel();
            ResumenCompra = new ResumenCompraEntradasViewModel();
            TarjetasGuardadas = new List<TarjetaGuardadaViewModel>();
        }

        [BindProperty(SupportsGet = true)]
        public int EventoId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int BloqueId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string AsientosSeleccionados { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public decimal Subtotal { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal Impuesto { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal Servicio { get; set; }

        [BindProperty(SupportsGet = true)]
        public decimal Total { get; set; }

        [BindProperty(SupportsGet = true)]
        public string PromoCode { get; set; } = string.Empty;

        [BindProperty]
        public DatosPagoEntradasViewModel DatosPago { get; set; }

        public ResumenCompraEntradasViewModel ResumenCompra { get; set; }

        public decimal PrecioTotal => Total;

        public bool MostrarMensajeExito { get; set; }

        [TempData]
        public string? MensajeError { get; set; }

        public List<TarjetaGuardadaViewModel> TarjetasGuardadas { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                if (EventoId <= 0 && TempData.ContainsKey("EventoId"))
                {
                    EventoId = TempData["EventoId"] as int? ?? 0;
                    BloqueId = TempData["BloqueId"] as int? ?? 0;
                    AsientosSeleccionados = TempData["AsientosSeleccionados"] as string ?? string.Empty;
                    Subtotal = TempData["Subtotal"] as decimal? ?? 0;
                    Impuesto = TempData["Impuesto"] as decimal? ?? 0;
                    Servicio = TempData["Servicio"] as decimal? ?? 0;
                    Total = TempData["Total"] as decimal? ?? 0;
                    PromoCode = TempData["PromoCode"] as string ?? string.Empty;
                }

                if (EventoId <= 0)
                {
                    return RedirectToPage("/Cliente/EventosDestacados");
                }

                await CargarDatosEventoAsync();
                await CargarDatosBloqueAsync();
                DatosPago.CantidadBoletos = string.IsNullOrEmpty(AsientosSeleccionados) ? 1 : AsientosSeleccionados.Split(',').Length;
                await CargarTarjetasGuardadasAsync();
                await PreRellenarDatosUsuarioAsync();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al cargar datos del evento {EventoId}");
                MensajeError = "Error al cargar los datos del evento. Por favor, inténtelo de nuevo.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    await CargarDatosEventoAsync();
                    await CargarDatosBloqueAsync();
                    await CargarTarjetasGuardadasAsync();
                    MensajeError = "Por favor, corrija los errores en el formulario.";
                    return Page();
                }

                if (Total <= 0)
                {
                    MensajeError = "El precio total no es válido.";
                    await CargarDatosEventoAsync();
                    await CargarDatosBloqueAsync();
                    await CargarTarjetasGuardadasAsync();
                    return Page();
                }

                decimal descuento = 0;
                int? idCodigoPromocional = null;
                if (!string.IsNullOrWhiteSpace(DatosPago.CodigoDescuento))
                {
                    (descuento, idCodigoPromocional) = await VerificarCodigoDescuentoAsync(DatosPago.CodigoDescuento);
                    if (descuento == 0 && !string.IsNullOrWhiteSpace(DatosPago.CodigoDescuento))
                    {
                        MensajeError = "Código de descuento inválido o expirado.";
                        await CargarDatosEventoAsync();
                        await CargarDatosBloqueAsync();
                        await CargarTarjetasGuardadasAsync();
                        return Page();
                    }
                }
                decimal precioFinal = Total * (1 - descuento);

                if (DatosPago.TarjetaSeleccionadaId.HasValue)
                {
                    bool saldoSuficiente = await VerificarSaldoTarjetaAsync(DatosPago.TarjetaSeleccionadaId.Value, precioFinal);
                    if (!saldoSuficiente)
                    {
                        MensajeError = "La tarjeta seleccionada no tiene saldo suficiente.";
                        await CargarDatosEventoAsync();
                        await CargarDatosBloqueAsync();
                        await CargarTarjetasGuardadasAsync();
                        return Page();
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(DatosPago.NumeroTarjeta) ||
                        string.IsNullOrEmpty(DatosPago.Vencimiento) ||
                        string.IsNullOrEmpty(DatosPago.CVV))
                    {
                        MensajeError = "Debe ingresar una tarjeta válida.";
                        await CargarDatosEventoAsync();
                        await CargarDatosBloqueAsync();
                        await CargarTarjetasGuardadasAsync();
                        return Page();
                    }

                    if (!IsValidCardNumber(DatosPago.NumeroTarjeta) ||
                        !IsValidExpirationDate(DatosPago.Vencimiento) ||
                        !IsValidCVV(DatosPago.CVV))
                    {
                        MensajeError = "Los datos de la tarjeta son inválidos.";
                        await CargarDatosEventoAsync();
                        await CargarDatosBloqueAsync();
                        await CargarTarjetasGuardadasAsync();
                        return Page();
                    }
                }

                bool asientosDisponibles = await VerificarYReservarAsientosAsync();
                if (!asientosDisponibles)
                {
                    MensajeError = "Uno o más asientos seleccionados ya no están disponibles.";
                    await CargarDatosEventoAsync();
                    await CargarDatosBloqueAsync();
                    await CargarTarjetasGuardadasAsync();
                    return Page();
                }

                int idCompra = await GuardarCompraAsync(precioFinal, descuento, idCodigoPromocional);

                if (idCompra > 0)
                {
                    return RedirectToPage("/Cliente/Factura", new { FacturaId = idCompra, TipoCompra = "entradas" });
                }

                MensajeError = "Error al procesar la compra.";
                await CargarDatosEventoAsync();
                await CargarDatosBloqueAsync();
                await CargarTarjetasGuardadasAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al procesar la compra de entradas: {ex.Message}");
                MensajeError = "Error al procesar la compra. Por favor, inténtelo de nuevo.";
                await CargarDatosEventoAsync();
                await CargarDatosBloqueAsync();
                await CargarTarjetasGuardadasAsync();
                return Page();
            }
        }

        private bool IsValidCardNumber(string numeroTarjeta)
        {
            string cleanedNumber = numeroTarjeta.Replace(" ", "").Replace("-", "");
            return cleanedNumber.Length == 16 && cleanedNumber.All(char.IsDigit);
        }

        private bool IsValidExpirationDate(string vencimiento)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(vencimiento, @"^(0[1-9]|1[0-2])\/[0-9]{2}$"))
                return false;

            var parts = vencimiento.Split('/');
            int month = int.Parse(parts[0]);
            int year = int.Parse(parts[1]) + 2000;
            var now = DateTime.Now;
            var expirationDate = new DateTime(year, month, 1);

            return expirationDate >= now;
        }

        private bool IsValidCVV(string cvv)
        {
            return cvv.Length >= 3 && cvv.Length <= 4 && cvv.All(char.IsDigit);
        }

        private async Task<bool> VerificarYReservarAsientosAsync()
        {
            if (string.IsNullOrEmpty(AsientosSeleccionados))
                return true;

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string tableName;
                string bloqueColumn;
                int lugarId = await GetLugarIdAsync(EventoId);
                switch (lugarId)
                {
                    case 1:
                        tableName = "AsientosEstadioNacional";
                        bloqueColumn = "id_bloqueEN";
                        break;
                    case 2:
                        tableName = "AsientosParqueViva";
                        bloqueColumn = "id_bloquePV";
                        break;
                    case 4:
                        tableName = "AsientosTEATROMelicoSalazar";
                        bloqueColumn = "id_bloqueTMS";
                        break;
                    case 3:
                        tableName = "AsientosCEP";
                        bloqueColumn = "id_bloqueCEP";
                        break;
                    default:
                        tableName = "Asientos";
                        bloqueColumn = "id_bloque";
                        break;
                }

                var asientos = AsientosSeleccionados.Split(',');
                string query = $@"
                    SELECT COUNT(*) 
                    FROM {tableName} 
                    WHERE {bloqueColumn} = @BloqueId 
                    AND numero_asiento IN ({string.Join(",", asientos.Select((_, i) => $"@Asiento{i}"))})
                    AND esta_disponible = 1";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@BloqueId", BloqueId);
                    for (int i = 0; i < asientos.Length; i++)
                    {
                        command.Parameters.AddWithValue($"@Asiento{i}", asientos[i].Trim());
                    }

                    int availableCount = Convert.ToInt32(await command.ExecuteScalarAsync());
                    if (availableCount != asientos.Length)
                        return false;

                    string updateQuery = $@"
                        UPDATE {tableName}
                        SET esta_disponible = 0
                        WHERE {bloqueColumn} = @BloqueId
                        AND numero_asiento IN ({string.Join(",", asientos.Select((_, i) => $"@Asiento{i}"))})";

                    using (var updateCommand = new SqlCommand(updateQuery, connection))
                    {
                        updateCommand.Parameters.AddWithValue("@BloqueId", BloqueId);
                        for (int i = 0; i < asientos.Length; i++)
                        {
                            updateCommand.Parameters.AddWithValue($"@Asiento{i}", asientos[i].Trim());
                        }

                        await updateCommand.ExecuteNonQueryAsync();
                    }
                }

                return true;
            }
        }

        private async Task<int> GetLugarIdAsync(int eventoId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = "SELECT id_lugar FROM Eventos WHERE id_evento = @EventoId";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@EventoId", eventoId);
                    var result = await command.ExecuteScalarAsync();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
        }

        private async Task CargarDatosEventoAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT e.nombre, e.fecha, e.lugar
                    FROM Eventos e
                    WHERE e.id_evento = @EventoId";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@EventoId", EventoId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            ResumenCompra.Evento = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                            ResumenCompra.Fecha = reader.GetDateTime(1);
                            ResumenCompra.Lugar = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                        }
                        else
                        {
                            throw new Exception($"No se encontró el evento con ID {EventoId}");
                        }
                    }
                }
            }
        }

        private async Task CargarDatosBloqueAsync()
        {
            if (BloqueId <= 0)
            {
                ResumenCompra.TipoBoleto = "General";
                ResumenCompra.AsientoZona = "Zona General";
                return;
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT nombre_bloque
                    FROM (
                        SELECT id_bloqueEN as id, nombre_bloque FROM BloqueEstadioNacional
                        UNION ALL
                        SELECT id_bloqueTMS as id, nombre_bloque FROM BloqueTEATROMS
                        UNION ALL
                        SELECT id_bloquePV as id, nombre_bloque FROM BloqueParqueViva
                        UNION ALL
                        SELECT id_bloqueCEP as id, nombre_bloque FROM BloqueCEP
                        UNION ALL
                        SELECT id_bloque as id, nombre_bloque FROM Bloques
                    ) AS AllBloques
                    WHERE id = @BloqueId";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@BloqueId", BloqueId);

                    var result = await command.ExecuteScalarAsync();
                    ResumenCompra.TipoBoleto = result != null && result != DBNull.Value ? result.ToString() : "General";
                }
            }

            ResumenCompra.AsientoZona = string.IsNullOrEmpty(AsientosSeleccionados)
                ? "Zona General"
                : $"Asientos: {AsientosSeleccionados}";
        }

        private async Task CargarTarjetasGuardadasAsync()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");

            if (!userId.HasValue)
            {
                string? userIdStr = Request.Cookies["UserId"];
                if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                {
                    userId = cookieUserId;
                    HttpContext.Session.SetInt32("UserId", cookieUserId);
                }
                else
                {
                    return;
                }
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    bool existeColumnaSaldo = false;
                    string queryVerificarColumna = @"
                        SELECT COUNT(*)
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME = 'TarjetasUsuario' AND COLUMN_NAME = 'Saldo'";

                    using (var commandVerificar = new SqlCommand(queryVerificarColumna, connection))
                    {
                        int count = Convert.ToInt32(await commandVerificar.ExecuteScalarAsync());
                        existeColumnaSaldo = count > 0;
                    }

                    if (!existeColumnaSaldo)
                    {
                        string queryAgregarColumna = @"
                            ALTER TABLE TarjetasUsuario
                            ADD Saldo DECIMAL(10, 2) DEFAULT 1000000";

                        using (var commandAgregar = new SqlCommand(queryAgregarColumna, connection))
                        {
                            await commandAgregar.ExecuteNonQueryAsync();
                        }
                    }

                    string query = @"
                        SELECT IdTarjeta, TipoTarjeta, NumeroTarjeta, Vencimiento, ISNULL(Saldo, 1000000) as Saldo
                        FROM TarjetasUsuario
                        WHERE IdUsuario = @IdUsuario";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdUsuario", userId.Value);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var tarjeta = new TarjetaGuardadaViewModel
                                {
                                    IdTarjeta = reader.GetInt32(0),
                                    TipoTarjeta = reader.IsDBNull(1) ? "Billetera Virtual" : reader.GetString(1),
                                    NumeroCompleto = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    Vencimiento = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                    Saldo = reader.GetDecimal(4)
                                };

                                tarjeta.UltimosDigitos = tarjeta.NumeroCompleto.Length > 4
                                    ? tarjeta.NumeroCompleto.Substring(tarjeta.NumeroCompleto.Length - 4)
                                    : tarjeta.NumeroCompleto;

                                TarjetasGuardadas.Add(tarjeta);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al cargar tarjetas guardadas para usuario ID: {userId}");
            }
        }

        private async Task PreRellenarDatosUsuarioAsync()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");

            if (!userId.HasValue)
            {
                string? userIdStr = Request.Cookies["UserId"];
                if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                {
                    userId = cookieUserId;
                    HttpContext.Session.SetInt32("UserId", cookieUserId);
                }
                else
                {
                    return;
                }
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT nombre, apellidos, correo
                        FROM Usuarios
                        WHERE id_usuario = @UserId";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId.Value);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                string nombre = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                                string apellidos = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                                string correo = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);

                                DatosPago.NombreCompleto = $"{nombre} {apellidos}".Trim();
                                DatosPago.CorreoElectronico = correo;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al cargar datos del usuario {userId}");
            }
        }

        private async Task<(decimal Descuento, int? IdCodigoPromocional)> VerificarCodigoDescuentoAsync(string? codigoDescuento)
        {
            if (string.IsNullOrWhiteSpace(codigoDescuento))
            {
                return (0, null);
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    int? userId = HttpContext.Session.GetInt32("UserId") ?? (Request.Cookies["UserId"] != null && int.TryParse(Request.Cookies["UserId"], out int cookieUserId) ? cookieUserId : null);

                    string query = @"
                        SELECT id_codigo, descuento
                        FROM CodigosPromocionales
                        WHERE codigo = @CodigoDescuento
                        AND estado = 'activo'
                        AND fecha_expiracion >= GETDATE()";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CodigoDescuento", codigoDescuento);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                int idCodigo = reader.GetInt32(0);
                                decimal descuento = reader.GetDecimal(1) / 100;
                                return (descuento, idCodigo);
                            }
                        }
                    }

                    if (userId.HasValue)
                    {
                        query = @"
                            SELECT cc.id_codigo, cp.descuento
                            FROM ComprasCodigos cc
                            JOIN CodigosPromocionales cp ON cc.id_codigo = cp.id_codigo
                            WHERE cc.codigo_generado = @CodigoDescuento
                            AND cc.id_usuario = @IdUsuario
                            AND cp.estado = 'activo'
                            AND cp.fecha_expiracion >= GETDATE()";

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@CodigoDescuento", codigoDescuento);
                            command.Parameters.AddWithValue("@IdUsuario", userId.Value);

                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    int idCodigo = reader.GetInt32(0);
                                    decimal descuento = reader.GetDecimal(1) / 100;
                                    return (descuento, idCodigo);
                                }
                            }
                        }
                    }

                    return (0, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al verificar código de descuento: {codigoDescuento}");
                return (0, null);
            }
        }

        private async Task<bool> VerificarSaldoTarjetaAsync(int idTarjeta, decimal monto)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT ISNULL(Saldo, 1000000) as Saldo
                        FROM TarjetasUsuario
                        WHERE IdTarjeta = @IdTarjeta";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdTarjeta", idTarjeta);

                        var result = await command.ExecuteScalarAsync();
                        if (result == null || result == DBNull.Value)
                        {
                            return false;
                        }

                        decimal saldo = Convert.ToDecimal(result);
                        return saldo >= monto;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al verificar saldo de tarjeta ID: {idTarjeta}: {ex.Message}");
                return false;
            }
        }

        private async Task DescontarSaldoTarjetaAsync(int idTarjeta, decimal monto, SqlConnection connection, SqlTransaction transaction)
        {
            string query = @"
                UPDATE TarjetasUsuario
                SET Saldo = ISNULL(Saldo, 1000000) - @Monto
                WHERE IdTarjeta = @IdTarjeta";

            using (var command = new SqlCommand(query, connection, transaction))
            {
                command.Parameters.AddWithValue("@IdTarjeta", idTarjeta);
                command.Parameters.AddWithValue("@Monto", monto);
                int rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    throw new Exception($"No se pudo actualizar el saldo de la tarjeta con ID: {idTarjeta}.");
                }
            }
        }

        private async Task<int> GuardarCompraAsync(decimal precioFinal, decimal descuento, int? idCodigoPromocional)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
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
                            }
                        }

                        int? idTarjeta = DatosPago.TarjetaSeleccionadaId;
                        if (idTarjeta.HasValue)
                        {
                            await DescontarSaldoTarjetaAsync(idTarjeta.Value, precioFinal, connection, transaction);
                        }
                        else
                        {
                            string insertTarjetaQuery = @"
                                INSERT INTO TarjetasUsuario (IdUsuario, TipoTarjeta, NumeroTarjeta, Vencimiento, CVV, Saldo)
                                VALUES (@IdUsuario, @TipoTarjeta, @NumeroTarjeta, @Vencimiento, @CVV, @Saldo);
                                SELECT SCOPE_IDENTITY();";

                            using (var command = new SqlCommand(insertTarjetaQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@IdUsuario", userId.HasValue ? (object)userId.Value : DBNull.Value);
                                command.Parameters.AddWithValue("@TipoTarjeta", "Tarjeta Crédito");
                                command.Parameters.AddWithValue("@NumeroTarjeta", DatosPago.NumeroTarjeta.Replace(" ", ""));
                                command.Parameters.AddWithValue("@Vencimiento", DatosPago.Vencimiento);
                                command.Parameters.AddWithValue("@CVV", DatosPago.CVV);
                                command.Parameters.AddWithValue("@Saldo", 1000000);

                                idTarjeta = Convert.ToInt32(await command.ExecuteScalarAsync());
                            }
                        }

                        string insertPagoQuery = @"
                            INSERT INTO PagosEntradas (id_compra, metodo_pago, numero_tarjeta, propietario_tarjeta, vencimiento, cvv, estado, fecha_pago, subtotal, impuesto, servicio, total_pagado, id_codigo_promocional, descuento_aplicado)
                            VALUES (@IdCompra, @MetodoPago, @NumeroTarjeta, @PropietarioTarjeta, @Vencimiento, @CVV, @Estado, @FechaPago, @Subtotal, @Impuesto, @Servicio, @TotalPagado, @IdCodigoPromocional, @DescuentoAplicado);
                            SELECT SCOPE_IDENTITY();";

                        int idPago;
                        using (var command = new SqlCommand(insertPagoQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@IdCompra", 0);
                            command.Parameters.AddWithValue("@MetodoPago", idTarjeta.HasValue ? "Tarjeta Virtual" : "Tarjeta Crédito");
                            command.Parameters.AddWithValue("@NumeroTarjeta", DatosPago.NumeroTarjeta.Replace(" ", "") ?? "****");
                            command.Parameters.AddWithValue("@PropietarioTarjeta", DatosPago.NombreCompleto);
                            command.Parameters.AddWithValue("@Vencimiento", DatosPago.Vencimiento ?? "12/99");
                            command.Parameters.AddWithValue("@CVV", DatosPago.CVV ?? "000");
                            command.Parameters.AddWithValue("@Estado", "aprobado");
                            command.Parameters.AddWithValue("@FechaPago", DateTime.Now);
                            command.Parameters.AddWithValue("@Subtotal", Subtotal);
                            command.Parameters.AddWithValue("@Impuesto", Impuesto);
                            command.Parameters.AddWithValue("@Servicio", Servicio);
                            command.Parameters.AddWithValue("@TotalPagado", precioFinal);
                            command.Parameters.AddWithValue("@IdCodigoPromocional", idCodigoPromocional.HasValue ? (object)idCodigoPromocional.Value : DBNull.Value);
                            command.Parameters.AddWithValue("@DescuentoAplicado", descuento * Total);

                            idPago = Convert.ToInt32(await command.ExecuteScalarAsync());
                        }

                        string insertCompraQuery = @"
                            INSERT INTO Compras (id_evento, id_bloque, numero_asiento, tipo_entrada, 
                                              precio_entrada, id_usuario, total, estado, fecha, 
                                              numero_factura, id_codigo_promocional, descuento_aplicado, id_pago)
                            VALUES (@IdEvento, @IdBloque, @NumeroAsiento, @TipoEntrada, 
                                   @PrecioEntrada, @IdUsuario, @Total, @Estado, @Fecha, 
                                   @NumeroFactura, @IdCodigoPromocional, @DescuentoAplicado, @IdPago);
                            SELECT SCOPE_IDENTITY();";

                        int idCompra;
                        using (var command = new SqlCommand(insertCompraQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@IdEvento", EventoId);
                            command.Parameters.AddWithValue("@IdBloque", BloqueId);
                            command.Parameters.AddWithValue("@NumeroAsiento", AsientosSeleccionados);
                            command.Parameters.AddWithValue("@TipoEntrada", ResumenCompra.TipoBoleto ?? "General");
                            command.Parameters.AddWithValue("@PrecioEntrada", Subtotal);
                            command.Parameters.AddWithValue("@IdUsuario", userId.HasValue ? (object)userId.Value : DBNull.Value);
                            command.Parameters.AddWithValue("@Total", precioFinal);
                            command.Parameters.AddWithValue("@Estado", "pagado");
                            command.Parameters.AddWithValue("@Fecha", DateTime.Now);
                            command.Parameters.AddWithValue("@NumeroFactura", $"FAC-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}");
                            command.Parameters.AddWithValue("@IdCodigoPromocional", idCodigoPromocional.HasValue ? (object)idCodigoPromocional.Value : DBNull.Value);
                            command.Parameters.AddWithValue("@DescuentoAplicado", descuento * Total);
                            command.Parameters.AddWithValue("@IdPago", idPago);

                            idCompra = Convert.ToInt32(await command.ExecuteScalarAsync());
                        }

                        string updatePagoQuery = @"
                            UPDATE PagosEntradas
                            SET id_compra = @IdCompra
                            WHERE id_pago = @IdPago";

                        using (var command = new SqlCommand(updatePagoQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@IdCompra", idCompra);
                            command.Parameters.AddWithValue("@IdPago", idPago);
                            await command.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        return idCompra;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        _logger.LogError(ex, $"Error al guardar la compra: {ex.Message}");
                        throw;
                    }
                }
            }
        }
    }

    public class DatosPagoEntradasViewModel
    {
        [Required(ErrorMessage = "El nombre completo es obligatorio")]
        [Display(Name = "Nombre completo")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es obligatorio")]
        [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido")]
        [Display(Name = "Correo electrónico")]
        public string CorreoElectronico { get; set; } = string.Empty;

        [Display(Name = "Número de tarjeta")]
        public string NumeroTarjeta { get; set; } = string.Empty;

        [Display(Name = "Fecha de vencimiento")]
        public string Vencimiento { get; set; } = string.Empty;

        [Display(Name = "CVV")]
        public string CVV { get; set; } = string.Empty;

        [Display(Name = "Cantidad de boletos")]
        public int CantidadBoletos { get; set; } = 1;

        [Display(Name = "Código de descuento")]
        public string? CodigoDescuento { get; set; }

        public int? TarjetaSeleccionadaId { get; set; }
    }

    public class ResumenCompraEntradasViewModel
    {
        public string Evento { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Lugar { get; set; } = string.Empty;
        public string TipoBoleto { get; set; } = string.Empty;
        public string AsientoZona { get; set; } = string.Empty;
    }

    public class TarjetaGuardadaViewModel
    {
        public int IdTarjeta { get; set; }
        public string TipoTarjeta { get; set; } = "Billetera Virtual";
        public string NumeroCompleto { get; set; } = string.Empty;
        public string UltimosDigitos { get; set; } = string.Empty;
        public string Vencimiento { get; set; } = string.Empty;
        public decimal Saldo { get; set; }
    }
}