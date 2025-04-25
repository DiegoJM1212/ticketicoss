using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ticketicos.Services;
using Microsoft.AspNetCore.Http;

namespace ticketicos.Pages.Cliente
{
    public class PagoCodigosModel : PageModel
    {
        private readonly ILogger<PagoCodigosModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;
        private readonly string _connectionString;

        public PagoCodigosModel(
            ILogger<PagoCodigosModel> logger,
            IConfiguration configuration,
            EmailService emailService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        [BindProperty(SupportsGet = true)]
        public int CodigoId { get; set; }

        [BindProperty]
        public DatosPagoViewModel DatosPago { get; set; } = new DatosPagoViewModel();

        public ResumenCompraViewModel ResumenCompra { get; set; } = new ResumenCompraViewModel();

        public decimal PrecioTotal { get; set; }

        public bool MostrarMensajeExito { get; set; }

        [TempData]
        public string? MensajeError { get; set; }

        [BindProperty]
        public int? TarjetaSeleccionadaId { get; set; }

        public List<TarjetaViewModel> TarjetasUsuario { get; set; } = new List<TarjetaViewModel>();

        public async Task<IActionResult> OnGetAsync()
        {
            if (CodigoId <= 0)
            {
                return RedirectToPage("/Cliente/CodigosPromo");
            }

            try
            {
                // Cargar datos del código promocional
                await CargarDatosCodigoAsync();

                // Cargar tarjetas del usuario
                await CargarTarjetasUsuarioAsync();

                // Pre-rellenar datos del usuario si está disponible
                await PreRellenarDatosUsuarioAsync();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al cargar datos del código promocional {CodigoId}");
                MensajeError = "Error al cargar los datos del código promocional. Por favor, inténtelo de nuevo.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                // Validar el modelo
                if (!ModelState.IsValid)
                {
                    await CargarDatosCodigoAsync();
                    await CargarTarjetasUsuarioAsync();
                    return Page();
                }

                // Cargar datos del código para tener el precio total
                await CargarDatosCodigoAsync();

                // Validar que PrecioTotal sea mayor que 0
                if (PrecioTotal <= 0)
                {
                    _logger.LogWarning($"El PrecioTotal es {PrecioTotal}. No se puede procesar la compra.");
                    MensajeError = "El precio total no es válido. Por favor, inténtelo de nuevo.";
                    await CargarTarjetasUsuarioAsync();
                    return Page();
                }

                // Verificar si se seleccionó una tarjeta guardada
                if (TarjetaSeleccionadaId.HasValue)
                {
                    // Verificar si la tarjeta tiene saldo suficiente
                    bool saldoSuficiente = await VerificarSaldoTarjetaAsync(TarjetaSeleccionadaId.Value, PrecioTotal);
                    if (!saldoSuficiente)
                    {
                        MensajeError = "La tarjeta seleccionada no tiene saldo suficiente para realizar esta compra.";
                        await CargarTarjetasUsuarioAsync();
                        return Page();
                    }
                }

                // Generar un código promocional único
                string codigoGenerado = GenerarCodigoUnico();

                // Guardar la compra en la base de datos y descontar el saldo de la tarjeta
                int idCompra = await GuardarCompraYDescontarSaldoAsync(codigoGenerado);

                if (idCompra > 0)
                {
                    // Redirigir a la página de factura
                    return RedirectToPage("/Cliente/Factura", new { FacturaId = idCompra, TipoCompra = "codigos" });
                }
                else
                {
                    MensajeError = "Error al procesar la compra. Por favor, inténtelo de nuevo.";
                    await CargarTarjetasUsuarioAsync();
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al procesar la compra del código promocional: {ex.Message}");
                MensajeError = "Error al procesar la compra. Por favor, inténtelo de nuevo.";
                await CargarDatosCodigoAsync();
                await CargarTarjetasUsuarioAsync();
                return Page();
            }
        }

        private async Task PreRellenarDatosUsuarioAsync()
        {
            // Obtener el ID del usuario de la sesión
            int? userId = HttpContext.Session.GetInt32("UserId");

            // Si no está en la sesión, intentar obtenerlo de la cookie
            if (!userId.HasValue)
            {
                string? userIdStr = Request.Cookies["UserId"];
                if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                {
                    userId = cookieUserId;
                    HttpContext.Session.SetInt32("UserId", cookieUserId);
                }
            }

            // Si tenemos un ID de usuario, cargar sus datos
            if (userId.HasValue)
            {
                try
                {
                    using (SqlConnection connection = new SqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        string query = @"
                            SELECT nombre, apellidos, correo
                            FROM Usuarios
                            WHERE id_usuario = @UserId";

                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@UserId", userId.Value);

                            using (SqlDataReader reader = await command.ExecuteReaderAsync())
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
                    // No lanzamos excepción para que la página siga funcionando
                }
            }
        }

        private async Task CargarDatosCodigoAsync()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Verificar si existen las columnas precio, descripcion y validez en la tabla CodigosPromocionales
                bool existeColumnaPrecio = false;
                bool existeColumnaDescripcion = false;
                bool existeColumnaValidez = false;

                string queryVerificarColumnas = @"
                    SELECT 
                        SUM(CASE WHEN COLUMN_NAME = 'precio' THEN 1 ELSE 0 END) AS tiene_precio,
                        SUM(CASE WHEN COLUMN_NAME = 'descripcion' THEN 1 ELSE 0 END) AS tiene_descripcion,
                        SUM(CASE WHEN COLUMN_NAME = 'validez' THEN 1 ELSE 0 END) AS tiene_validez
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'CodigosPromocionales'";

                using (SqlCommand commandVerificar = new SqlCommand(queryVerificarColumnas, connection))
                {
                    using (SqlDataReader reader = await commandVerificar.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            existeColumnaPrecio = reader.GetInt32(0) > 0;
                            existeColumnaDescripcion = reader.GetInt32(1) > 0;
                            existeColumnaValidez = reader.GetInt32(2) > 0;
                        }
                    }
                }

                // Agregar las columnas que faltan
                if (!existeColumnaPrecio)
                {
                    string queryAgregarPrecio = @"
                        ALTER TABLE CodigosPromocionales
                        ADD precio DECIMAL(10, 2) DEFAULT 5000";

                    using (SqlCommand commandAgregar = new SqlCommand(queryAgregarPrecio, connection))
                    {
                        await commandAgregar.ExecuteNonQueryAsync();
                    }
                }

                if (!existeColumnaDescripcion)
                {
                    string queryAgregarDescripcion = @"
                        ALTER TABLE CodigosPromocionales
                        ADD descripcion VARCHAR(100) DEFAULT 'Código promocional'";

                    using (SqlCommand commandAgregar = new SqlCommand(queryAgregarDescripcion, connection))
                    {
                        await commandAgregar.ExecuteNonQueryAsync();
                    }
                }

                if (!existeColumnaValidez)
                {
                    string queryAgregarValidez = @"
                        ALTER TABLE CodigosPromocionales
                        ADD validez VARCHAR(100) DEFAULT 'Todos los eventos'";

                    using (SqlCommand commandAgregar = new SqlCommand(queryAgregarValidez, connection))
                    {
                        await commandAgregar.ExecuteNonQueryAsync();
                    }
                }

                // Construir la consulta según las columnas existentes
                string query = "SELECT codigo, descuento";

                if (existeColumnaPrecio)
                    query += ", precio";

                if (existeColumnaDescripcion)
                    query += ", descripcion";

                if (existeColumnaValidez)
                    query += ", validez";

                query += " FROM CodigosPromocionales WHERE id_codigo = @CodigoId";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CodigoId", CodigoId);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string codigo = reader.GetString(0);
                            decimal descuento = reader.GetDecimal(1);

                            // Obtener precio si existe la columna
                            decimal precio = 5000; // Valor predeterminado
                            if (existeColumnaPrecio)
                            {
                                int precioOrdinal = reader.GetOrdinal("precio");
                                if (!reader.IsDBNull(precioOrdinal))
                                {
                                    precio = reader.GetDecimal(precioOrdinal);
                                }
                            }

                            // Obtener descripción si existe la columna
                            string descripcion = "Código promocional"; // Valor predeterminado
                            if (existeColumnaDescripcion)
                            {
                                int descripcionOrdinal = reader.GetOrdinal("descripcion");
                                if (!reader.IsDBNull(descripcionOrdinal))
                                {
                                    descripcion = reader.GetString(descripcionOrdinal);
                                }
                            }

                            // Obtener validez si existe la columna
                            string validez = "Todos los eventos"; // Valor predeterminado
                            if (existeColumnaValidez)
                            {
                                int validezOrdinal = reader.GetOrdinal("validez");
                                if (!reader.IsDBNull(validezOrdinal))
                                {
                                    validez = reader.GetString(validezOrdinal);
                                }
                            }

                            ResumenCompra.TipoCodigo = $"{descripcion} - {descuento:C0} de descuento";
                            ResumenCompra.ValidoPara = validez;
                            ResumenCompra.Codigo = codigo;
                            PrecioTotal = precio;
                        }
                        else
                        {
                            throw new Exception($"No se encontró el código promocional con ID {CodigoId}");
                        }
                    }
                }
            }
        }

        private async Task CargarTarjetasUsuarioAsync()
        {
            // Obtener el ID del usuario de la sesión
            int? userId = HttpContext.Session.GetInt32("UserId");

            // Si no está en la sesión, intentar obtenerlo de la cookie
            if (!userId.HasValue)
            {
                string? userIdStr = Request.Cookies["UserId"];
                if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                {
                    // Si se encuentra en la cookie, restaurar a la sesión también
                    userId = cookieUserId;
                    HttpContext.Session.SetInt32("UserId", cookieUserId);
                }
            }

            // Si el usuario está autenticado, cargar sus tarjetas
            if (userId.HasValue)
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Verificar si existe la columna Saldo en la tabla TarjetasUsuario
                    bool existeColumnaSaldo = false;
                    string queryVerificarColumna = @"
                        SELECT COUNT(*)
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME = 'TarjetasUsuario' AND COLUMN_NAME = 'Saldo'";

                    using (SqlCommand commandVerificar = new SqlCommand(queryVerificarColumna, connection))
                    {
                        int count = Convert.ToInt32(await commandVerificar.ExecuteScalarAsync());
                        existeColumnaSaldo = count > 0;
                    }

                    // Si no existe la columna Saldo, la agregamos
                    if (!existeColumnaSaldo)
                    {
                        string queryAgregarColumna = @"
                            ALTER TABLE TarjetasUsuario
                            ADD Saldo DECIMAL(10, 2) DEFAULT 1000000";

                        using (SqlCommand commandAgregar = new SqlCommand(queryAgregarColumna, connection))
                        {
                            await commandAgregar.ExecuteNonQueryAsync();
                        }
                    }

                    // Consulta para obtener las tarjetas del usuario
                    string query = @"
                        SELECT IdTarjeta, TipoTarjeta, NumeroTarjeta, Vencimiento, 
                               ISNULL(Saldo, 1000000) as Saldo
                        FROM TarjetasUsuario
                        WHERE IdUsuario = @UserId";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId.Value);

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int idTarjeta = reader.GetInt32(0);
                                string tipoTarjeta = reader.GetString(1);
                                string numeroTarjeta = reader.GetString(2);
                                string vencimiento = reader.GetString(3);
                                decimal saldo = reader.GetDecimal(4);

                                // Mostrar solo los últimos 4 dígitos de la tarjeta
                                string numeroEnmascarado = $"**** **** **** {numeroTarjeta.Substring(Math.Max(0, numeroTarjeta.Length - 4))}";

                                TarjetasUsuario.Add(new TarjetaViewModel
                                {
                                    IdTarjeta = idTarjeta,
                                    TipoTarjeta = tipoTarjeta,
                                    NumeroTarjeta = numeroEnmascarado,
                                    Vencimiento = vencimiento,
                                    Saldo = saldo
                                });
                            }
                        }
                    }
                }
            }
        }

        private async Task<bool> VerificarSaldoTarjetaAsync(int idTarjeta, decimal monto)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT ISNULL(Saldo, 1000000) as Saldo
                    FROM TarjetasUsuario
                    WHERE IdTarjeta = @IdTarjeta";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@IdTarjeta", idTarjeta);

                    var result = await command.ExecuteScalarAsync();
                    if (result == null || result == DBNull.Value)
                    {
                        _logger.LogWarning($"No se encontró la tarjeta con ID: {idTarjeta}.");
                        return false;
                    }

                    decimal saldo = Convert.ToDecimal(result);
                    _logger.LogInformation($"Saldo actual de la tarjeta ID: {idTarjeta} es {saldo}, Monto requerido: {monto}");
                    return saldo >= monto;
                }
            }
        }

        private async Task DescontarSaldoTarjetaAsync(int idTarjeta, decimal monto, SqlConnection connection, SqlTransaction transaction)
        {
            _logger.LogInformation($"Iniciando descuento de saldo. Tarjeta ID: {idTarjeta}, Monto: {monto}");

            string queryActualizarSaldo = @"
                UPDATE TarjetasUsuario
                SET Saldo = ISNULL(Saldo, 1000000) - @Monto
                WHERE IdTarjeta = @IdTarjeta";

            using (SqlCommand commandActualizar = new SqlCommand(queryActualizarSaldo, connection, transaction))
            {
                commandActualizar.Parameters.AddWithValue("@IdTarjeta", idTarjeta);
                commandActualizar.Parameters.AddWithValue("@Monto", monto);
                int rowsAffected = await commandActualizar.ExecuteNonQueryAsync();

                _logger.LogInformation($"Filas afectadas  Filas afectadas al actualizar saldo: {rowsAffected}");

                if (rowsAffected == 0)
                {
                    _logger.LogWarning($"No se encontró la tarjeta con ID: {idTarjeta} o no se pudo actualizar el saldo.");
                    throw new Exception($"No se pudo actualizar el saldo de la tarjeta con ID: {idTarjeta}.");
                }

                _logger.LogInformation($"Saldo descontado correctamente de la tarjeta ID: {idTarjeta}, Monto: {monto}");
            }
        }

        private string GenerarCodigoUnico()
        {
            // Generar un código alfanumérico único de 8 caracteres
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var codigo = new char[8];

            for (int i = 0; i < codigo.Length; i++)
            {
                codigo[i] = chars[random.Next(chars.Length)];
            }

            return new string(codigo);
        }

        private async Task<int> GuardarCompraYDescontarSaldoAsync(string codigoGenerado)
        {
            // Verificar si existe la tabla ComprasCodigos
            bool existeTablaComprasCodigos = false;
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string queryVerificarTabla = @"
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_NAME = 'ComprasCodigos'";

                using (SqlCommand commandVerificar = new SqlCommand(queryVerificarTabla, connection))
                {
                    int count = Convert.ToInt32(await commandVerificar.ExecuteScalarAsync());
                    existeTablaComprasCodigos = count > 0;
                }

                // Si no existe la tabla ComprasCodigos, la creamos
                if (!existeTablaComprasCodigos)
                {
                    string queryCrearTabla = @"
                        CREATE TABLE ComprasCodigos (
                            id_compra INT PRIMARY KEY IDENTITY(1,1),
                            id_codigo INT,
                            id_usuario INT,
                            nombre_cliente VARCHAR(100),
                            correo_electronico VARCHAR(100),
                            fecha DATETIME,
                            total DECIMAL(10, 2),
                            codigo_generado VARCHAR(20),
                            id_tarjeta INT
                        )";

                    using (SqlCommand commandCrear = new SqlCommand(queryCrearTabla, connection))
                    {
                        await commandCrear.ExecuteNonQueryAsync();
                    }
                }
            }

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Iniciar una transacción para asegurar la integridad de los datos
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Obtener el ID del usuario de la sesión (si está disponible)
                        int? userId = HttpContext.Session.GetInt32("UserId");

                        // Si no está en la sesión, intentar obtenerlo de la cookie
                        if (!userId.HasValue)
                        {
                            string? userIdStr = Request.Cookies["UserId"];
                            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                            {
                                userId = cookieUserId;
                            }
                        }

                        _logger.LogInformation($"Procesando compra para usuario ID: {userId}, Código ID: {CodigoId}, Tarjeta ID: {TarjetaSeleccionadaId}");

                        // Descontar saldo si se seleccionó una tarjeta
                        if (TarjetaSeleccionadaId.HasValue)
                        {
                            _logger.LogInformation($"Verificando saldo para tarjeta ID: {TarjetaSeleccionadaId}");
                            bool saldoSuficiente = await VerificarSaldoTarjetaAsync(TarjetaSeleccionadaId.Value, PrecioTotal);
                            if (!saldoSuficiente)
                            {
                                throw new Exception($"La tarjeta con ID {TarjetaSeleccionadaId} no tiene saldo suficiente.");
                            }

                            await DescontarSaldoTarjetaAsync(TarjetaSeleccionadaId.Value, PrecioTotal, connection, transaction);
                        }

                        // Insertar la compra en la tabla ComprasCodigos
                        string insertQuery = @"
                            INSERT INTO ComprasCodigos (id_codigo, id_usuario, nombre_cliente, correo_electronico, 
                                                      fecha, total, codigo_generado, id_tarjeta)
                            VALUES (@IdCodigo, @IdUsuario, @NombreCliente, @CorreoElectronico, 
                                   @Fecha, @Total, @CodigoGenerado, @IdTarjeta);
                            SELECT SCOPE_IDENTITY();";

                        using (SqlCommand command = new SqlCommand(insertQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@IdCodigo", CodigoId);
                            command.Parameters.AddWithValue("@IdUsuario", userId.HasValue ? (object)userId.Value : DBNull.Value);
                            command.Parameters.AddWithValue("@NombreCliente", DatosPago.NombreCompleto);
                            command.Parameters.AddWithValue("@CorreoElectronico", DatosPago.CorreoElectronico);
                            command.Parameters.AddWithValue("@Fecha", DateTime.Now);
                            command.Parameters.AddWithValue("@Total", PrecioTotal);
                            command.Parameters.AddWithValue("@CodigoGenerado", codigoGenerado);
                            command.Parameters.AddWithValue("@IdTarjeta", TarjetaSeleccionadaId.HasValue ? (object)TarjetaSeleccionadaId.Value : DBNull.Value);

                            // Obtener el ID de la compra insertada
                            var result = await command.ExecuteScalarAsync();
                            int idCompra = Convert.ToInt32(result);

                            _logger.LogInformation($"Compra registrada con ID: {idCompra}");

                            // Confirmar la transacción
                            transaction.Commit();
                            return idCompra;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Revertir la transacción en caso de error
                        _logger.LogError(ex, $"Error al guardar la compra del código promocional. Mensaje: {ex.Message}");
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }

    public class DatosPagoViewModel
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
    }

    public class ResumenCompraViewModel
    {
        public string TipoCodigo { get; set; } = string.Empty;
        public string ValidoPara { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
    }

    public class TarjetaViewModel
    {
        public int IdTarjeta { get; set; }
        public string TipoTarjeta { get; set; } = string.Empty;
        public string NumeroTarjeta { get; set; } = string.Empty;
        public string Vencimiento { get; set; } = string.Empty;
        public decimal Saldo { get; set; }
    }
}