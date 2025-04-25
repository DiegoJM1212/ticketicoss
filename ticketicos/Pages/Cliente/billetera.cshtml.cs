using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ticketicos.Data;
using ticketicos.Models;

namespace ticketicos.Pages.Cliente
{
    public class BilleteraModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BilleteraModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public BilleteraModel(ApplicationDbContext context, ILogger<BilleteraModel> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
            TarjetasUsuario = new List<ticketicos.Models.TarjetaUsuario>();
            MensajeExito = string.Empty;
            MensajeError = string.Empty;
            NuevaTarjeta = new ticketicos.Models.TarjetaUsuario();
        }

        public List<ticketicos.Models.TarjetaUsuario> TarjetasUsuario { get; set; }
        public string? MensajeExito { get; set; }
        public string? MensajeError { get; set; }

        [BindProperty]
        public ticketicos.Models.TarjetaUsuario NuevaTarjeta { get; set; }

        [BindProperty]
        public RecargaSaldoViewModel RecargaSaldo { get; set; } = new RecargaSaldoViewModel();

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                _logger.LogInformation($"ID de usuario en sesión: {(userId.HasValue ? userId.Value.ToString() : "No encontrado")}");

                if (!userId.HasValue)
                {
                    string? userIdStr = Request.Cookies["UserId"];
                    _logger.LogInformation($"Intentando obtener ID de usuario de cookie: {userIdStr ?? "No encontrado"}");

                    if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                    {
                        userId = cookieUserId;
                        HttpContext.Session.SetInt32("UserId", cookieUserId);
                        _logger.LogInformation($"Usuario {userId} autenticado desde cookie");
                    }
                }

                if (!userId.HasValue)
                {
                    _logger.LogWarning("No se pudo obtener el ID de usuario, redirigiendo a login");
                    return RedirectToPage("/Cliente/Login"); // Ruta absoluta desde Pages
                }

                // Verificar si hay mensajes en TempData
                if (TempData["MensajeExito"] != null)
                {
                    MensajeExito = TempData["MensajeExito"].ToString();
                }
                if (TempData["MensajeError"] != null)
                {
                    MensajeError = TempData["MensajeError"].ToString();
                }

                await VerificarColumnaSaldoAsync();
                await CargarTarjetasConSQLAsync(userId.Value);

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar la billetera virtual: {Message}", ex.Message);
                MensajeError = "Error al cargar las tarjetas. Por favor, intenta nuevamente.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAgregarTarjetaAsync()
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                _logger.LogInformation($"ID de usuario en sesión (AgregarTarjeta): {(userId.HasValue ? userId.Value.ToString() : "No encontrado")}");

                if (!userId.HasValue)
                {
                    string? userIdStr = Request.Cookies["UserId"];
                    _logger.LogInformation($"Intentando obtener ID de usuario de cookie (AgregarTarjeta): {userIdStr ?? "No encontrado"}");

                    if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                    {
                        userId = cookieUserId;
                        HttpContext.Session.SetInt32("UserId", cookieUserId);
                        _logger.LogInformation($"Usuario {userId} autenticado desde cookie (AgregarTarjeta)");
                    }
                }

                if (!userId.HasValue)
                {
                    _logger.LogWarning("No se pudo obtener el ID de usuario, redirigiendo a login (AgregarTarjeta)");
                    return RedirectToPage("/Cliente/Login"); // Ruta absoluta desde Pages
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Modelo no válido al agregar tarjeta");
                    await CargarTarjetasConSQLAsync(userId.Value);
                    return Page();
                }

                await VerificarColumnaSaldoAsync();

                NuevaTarjeta.IdUsuario = userId.Value;
                NuevaTarjeta.Saldo = 100000;

                _logger.LogInformation($"Agregando nueva tarjeta para usuario {userId.Value}: Tipo={NuevaTarjeta.TipoTarjeta}, Número={NuevaTarjeta.NumeroTarjeta}");

                await AgregarTarjetaConSQLAsync(NuevaTarjeta);

                TempData["MensajeExito"] = "Tarjeta agregada exitosamente a tu billetera virtual.";
                return RedirectToPage("/Cliente/Billetera"); // Ruta absoluta desde Pages
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar tarjeta:  {Message}", ex.Message);

                int? userId = HttpContext.Session.GetInt32("UserId");
                if (userId.HasValue)
                {
                    await CargarTarjetasConSQLAsync(userId.Value);
                }

                MensajeError = "Error al agregar la tarjeta. Por favor, intenta nuevamente.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostEliminarTarjetaAsync(int idTarjeta)
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                _logger.LogInformation($"ID de usuario en sesión (EliminarTarjeta): {(userId.HasValue ? userId.Value.ToString() : "No encontrado")}");

                if (!userId.HasValue)
                {
                    string? userIdStr = Request.Cookies["UserId"];
                    _logger.LogInformation($"Intentando obtener ID de usuario de cookie (EliminarTarjeta): {userIdStr ?? "No encontrado"}");

                    if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                    {
                        userId = cookieUserId;
                        HttpContext.Session.SetInt32("UserId", cookieUserId);
                        _logger.LogInformation($"Usuario {userId} autenticado desde cookie (EliminarTarjeta)");
                    }
                }

                if (!userId.HasValue)
                {
                    _logger.LogWarning("No se pudo obtener el ID de usuario, redirigiendo a login (EliminarTarjeta)");
                    return RedirectToPage("/Cliente/Login"); // Ruta absoluta desde Pages
                }

                _logger.LogInformation($"Intentando eliminar tarjeta ID: {idTarjeta} para usuario ID: {userId.Value}");

                await EliminarTarjetaConSQLAsync(idTarjeta, userId.Value);
                TempData["MensajeExito"] = "Tarjeta eliminada exitosamente de tu billetera virtual.";

                return RedirectToPage("/Cliente/Billetera"); // Ruta absoluta desde Pages
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar tarjeta: {Message}", ex.Message);
                TempData["MensajeError"] = "Error al eliminar la tarjeta. Por favor, intenta nuevamente.";
                return RedirectToPage("/Cliente/Billetera"); // Ruta absoluta desde Pages
            }
        }

        public async Task<IActionResult> OnPostRecargarSaldoAsync()
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                _logger.LogInformation($"ID de usuario en sesión (RecargarSaldo): {(userId.HasValue ? userId.Value.ToString() : "No encontrado")}");

                if (!userId.HasValue)
                {
                    string? userIdStr = Request.Cookies["UserId"];
                    _logger.LogInformation($"Intentando obtener ID de usuario de cookie (RecargarSaldo): {userIdStr ?? "No encontrado"}");

                    if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                    {
                        userId = cookieUserId;
                        HttpContext.Session.SetInt32("UserId", cookieUserId);
                        _logger.LogInformation($"Usuario {userId} autenticado desde cookie (RecargarSaldo)");
                    }
                }

                if (!userId.HasValue)
                {
                    _logger.LogWarning("No se pudo obtener el ID de usuario, redirigiendo a login (RecargarSaldo)");
                    return RedirectToPage("/Cliente/Login"); // Ruta absoluta desde Pages
                }

                // Validación adicional para el monto y la tarjeta
                if (RecargaSaldo.IdTarjeta <= 0)
                {
                    TempData["MensajeError"] = "Debe seleccionar una tarjeta válida.";
                    return RedirectToPage("/Cliente/Billetera");
                }

                if (RecargaSaldo.Monto <= 0)
                {
                    TempData["MensajeError"] = "El monto a recargar debe ser mayor que cero.";
                    return RedirectToPage("/Cliente/Billetera");
                }

                _logger.LogInformation($"Intentando recargar saldo en tarjeta ID: {RecargaSaldo.IdTarjeta} para usuario ID: {userId.Value}, Monto: {RecargaSaldo.Monto}");

                // Ejecutar la recarga dentro de una transacción para garantizar la consistencia
                decimal nuevoSaldo = await RecargarSaldoConSQLAsync(RecargaSaldo.IdTarjeta, userId.Value, RecargaSaldo.Monto);

                TempData["MensajeExito"] = $"Saldo recargado exitosamente. Nuevo saldo: ₡{nuevoSaldo:N0}";

                // Forzar una recarga completa para asegurar que se muestren los datos actualizados
                return RedirectToPage("/Cliente/Billetera"); // Ruta absoluta desde Pages
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al recargar saldo: {Message}", ex.Message);
                TempData["MensajeError"] = "Error al recargar el saldo: " + ex.Message;
                return RedirectToPage("/Cliente/Billetera"); // Ruta absoluta desde Pages
            }
        }

        private async Task VerificarColumnaSaldoAsync()
        {
            try
            {
                _logger.LogInformation("Verificando columna Saldo en tabla TarjetasUsuario");

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string queryVerificarColumna = @"
                        SELECT COUNT(*)
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME = 'TarjetasUsuario' AND COLUMN_NAME = 'Saldo'";

                    using (SqlCommand commandVerificar = new SqlCommand(queryVerificarColumna, connection))
                    {
                        int count = Convert.ToInt32(await commandVerificar.ExecuteScalarAsync());
                        bool existeColumnaSaldo = count > 0;

                        _logger.LogInformation($"Columna Saldo existe: {existeColumnaSaldo}");

                        if (!existeColumnaSaldo)
                        {
                            _logger.LogInformation("Agregando columna Saldo a la tabla TarjetasUsuario");

                            string queryAgregarColumna = @"
                                ALTER TABLE TarjetasUsuario
                                ADD Saldo DECIMAL(10, 2) DEFAULT 100000";

                            using (SqlCommand commandAgregar = new SqlCommand(queryAgregarColumna, connection))
                            {
                                await commandAgregar.ExecuteNonQueryAsync();
                                _logger.LogInformation("Columna Saldo agregada exitosamente");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar columna Saldo: {Message}", ex.Message);
                throw;
            }
        }

        private async Task CargarTarjetasConSQLAsync(int userId)
        {
            try
            {
                _logger.LogInformation($"Cargando tarjetas con SQL para el usuario {userId}");
                TarjetasUsuario.Clear();

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string queryVerificarTabla = @"
                        SELECT COUNT(*)
                        FROM INFORMATION_SCHEMA.TABLES
                        WHERE TABLE_NAME = 'TarjetasUsuario'";

                    using (SqlCommand commandVerificar = new SqlCommand(queryVerificarTabla, connection))
                    {
                        int count = Convert.ToInt32(await commandVerificar.ExecuteScalarAsync());
                        if (count == 0)
                        {
                            _logger.LogError("La tabla TarjetasUsuario no existe");
                            MensajeError = "Error al cargar las tarjetas: la tabla no existe.";
                            return;
                        }
                    }

                    string queryTodasTarjetas = "SELECT COUNT(*) FROM TarjetasUsuario";
                    using (SqlCommand commandTodasTarjetas = new SqlCommand(queryTodasTarjetas, connection))
                    {
                        int totalTarjetas = Convert.ToInt32(await commandTodasTarjetas.ExecuteScalarAsync());
                        _logger.LogInformation($"Total de tarjetas en la base de datos: {totalTarjetas}");
                    }

                    string query = @"
                        SELECT IdTarjeta, IdUsuario, TipoTarjeta, NumeroTarjeta, Vencimiento, CVV, 
                               ISNULL(Saldo, 100000) AS Saldo
                        FROM TarjetasUsuario
                        WHERE IdUsuario = @UserId";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var tarjeta = new TarjetaUsuario
                                {
                                    IdTarjeta = reader.GetInt32(0),
                                    IdUsuario = reader.GetInt32(1),
                                    TipoTarjeta = reader.GetString(2),
                                    NumeroTarjeta = reader.GetString(3),
                                    Vencimiento = reader.GetString(4),
                                    CVV = reader.GetString(5),
                                    Saldo = reader.GetDecimal(6)
                                };

                                TarjetasUsuario.Add(tarjeta);
                            }
                        }
                    }

                    _logger.LogInformation($"Se cargaron {TarjetasUsuario.Count} tarjetas para el usuario {userId} usando SQL directo");

                    if (TarjetasUsuario.Count == 0 && userId == 1)
                    {
                        _logger.LogWarning($"No se encontraron tarjetas para el usuario {userId}. Creando tarjeta de prueba...");
                        await CrearTarjetaPruebaAsync(userId);
                        await CargarTarjetasConSQLAsync(userId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar tarjetas con SQL: {Message}", ex.Message);
                throw;
            }
        }

        private async Task AgregarTarjetaConSQLAsync(TarjetaUsuario tarjeta)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        INSERT INTO TarjetasUsuario (IdUsuario, TipoTarjeta, NumeroTarjeta, Vencimiento, CVV, Saldo)
                        VALUES (@IdUsuario, @TipoTarjeta, @NumeroTarjeta, @Vencimiento, @CVV, @Saldo);
                        SELECT SCOPE_IDENTITY();";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdUsuario", tarjeta.IdUsuario);
                        command.Parameters.AddWithValue("@TipoTarjeta", tarjeta.TipoTarjeta);
                        command.Parameters.AddWithValue("@NumeroTarjeta", tarjeta.NumeroTarjeta);
                        command.Parameters.AddWithValue("@Vencimiento", tarjeta.Vencimiento);
                        command.Parameters.AddWithValue("@CVV", tarjeta.CVV);
                        command.Parameters.AddWithValue("@Saldo", tarjeta.Saldo);

                        var result = await command.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            tarjeta.IdTarjeta = Convert.ToInt32(result);
                        }
                    }
                }

                _logger.LogInformation($"Tarjeta agregada exitosamente con SQL directo. ID: {tarjeta.IdTarjeta}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar tarjeta con SQL: {Message}", ex.Message);
                throw;
            }
        }

        private async Task EliminarTarjetaConSQLAsync(int idTarjeta, int userId)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        DELETE FROM TarjetasUsuario
                        WHERE IdTarjeta = @IdTarjeta AND IdUsuario = @IdUsuario";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdTarjeta", idTarjeta);
                        command.Parameters.AddWithValue("@IdUsuario", userId);

                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        _logger.LogInformation($"Tarjeta eliminada con SQL directo. Filas afectadas: {rowsAffected}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar tarjeta con SQL: {Message}", ex.Message);
                throw;
            }
        }

        // MÉTODO CORREGIDO PARA RECARGAR SALDO
        private async Task<decimal> RecargarSaldoConSQLAsync(int idTarjeta, int userId, decimal monto)
        {
            decimal nuevoSaldo = 0;
            SqlConnection connection = null;
            SqlTransaction transaction = null;

            try
            {
                connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Iniciar una transacción para garantizar la consistencia
                transaction = connection.BeginTransaction();

                // Primero verificamos que la tarjeta exista y pertenezca al usuario
                string queryVerificar = @"
                    SELECT COUNT(*)
                    FROM TarjetasUsuario WITH (UPDLOCK, ROWLOCK)
                    WHERE IdTarjeta = @IdTarjeta AND IdUsuario = @IdUsuario";

                using (SqlCommand commandVerificar = new SqlCommand(queryVerificar, connection, transaction))
                {
                    commandVerificar.Parameters.AddWithValue("@IdTarjeta", idTarjeta);
                    commandVerificar.Parameters.AddWithValue("@IdUsuario", userId);

                    int count = Convert.ToInt32(await commandVerificar.ExecuteScalarAsync());
                    if (count == 0)
                    {
                        _logger.LogWarning($"No se encontró la tarjeta ID: {idTarjeta} para el usuario ID: {userId}");
                        throw new Exception("No se encontró la tarjeta seleccionada o no pertenece a este usuario.");
                    }
                }

                // Luego actualizamos el saldo
                string queryActualizar = @"
                    UPDATE TarjetasUsuario
                    SET Saldo = ISNULL(Saldo, 0) + @Monto
                    WHERE IdTarjeta = @IdTarjeta AND IdUsuario = @IdUsuario";

                using (SqlCommand commandActualizar = new SqlCommand(queryActualizar, connection, transaction))
                {
                    commandActualizar.Parameters.AddWithValue("@IdTarjeta", idTarjeta);
                    commandActualizar.Parameters.AddWithValue("@IdUsuario", userId);
                    commandActualizar.Parameters.AddWithValue("@Monto", monto);

                    int rowsAffected = await commandActualizar.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        _logger.LogWarning($"No se pudo actualizar el saldo de la tarjeta ID: {idTarjeta}");
                        throw new Exception("No se pudo actualizar el saldo de la tarjeta.");
                    }
                }

                // Finalmente obtenemos el nuevo saldo
                string queryObtenerSaldo = @"
                    SELECT Saldo
                    FROM TarjetasUsuario
                    WHERE IdTarjeta = @IdTarjeta";

                using (SqlCommand commandObtenerSaldo = new SqlCommand(queryObtenerSaldo, connection, transaction))
                {
                    commandObtenerSaldo.Parameters.AddWithValue("@IdTarjeta", idTarjeta);

                    object result = await commandObtenerSaldo.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        nuevoSaldo = Convert.ToDecimal(result);
                        _logger.LogInformation($"Saldo recargado con SQL directo. Nuevo saldo: {nuevoSaldo}");
                    }
                    else
                    {
                        _logger.LogWarning("No se pudo obtener el nuevo saldo después de la actualización");
                        throw new Exception("No se pudo obtener el nuevo saldo después de la actualización.");
                    }
                }

                // Si llegamos aquí, todo salió bien, hacemos commit de la transacción
                transaction.Commit();
                _logger.LogInformation("Transacción de recarga de saldo completada exitosamente");

                return nuevoSaldo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al recargar saldo con SQL: {Message}", ex.Message);

                // Si hay un error, hacemos rollback de la transacción
                transaction?.Rollback();

                throw;
            }
            finally
            {
                // Cerramos la conexión
                if (connection != null && connection.State == System.Data.ConnectionState.Open)
                {
                    connection.Close();
                }
            }
        }

        private async Task CrearTarjetaPruebaAsync(int userId)
        {
            try
            {
                _logger.LogInformation($"Creando tarjeta de prueba para usuario {userId}");

                var tarjetaPrueba = new TarjetaUsuario
                {
                    IdUsuario = userId,
                    TipoTarjeta = "American Express",
                    NumeroTarjeta = "1234 1234 1234 1234",
                    Vencimiento = "12/27",
                    CVV = "123",
                    Saldo = 100000
                };

                await AgregarTarjetaConSQLAsync(tarjetaPrueba);

                _logger.LogInformation("Tarjeta de prueba creada exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear tarjeta de prueba: {Message}", ex.Message);
                throw;
            }
        }

        private async Task<bool> VerificarTablaTarjetasAsync()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string queryVerificarTabla = @"
                        SELECT COUNT(*)
                        FROM INFORMATION_SCHEMA.TABLES
                        WHERE TABLE_NAME = 'TarjetasUsuario'";

                    using (SqlCommand command = new SqlCommand(queryVerificarTabla, connection))
                    {
                        int count = Convert.ToInt32(await command.ExecuteScalarAsync());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar la tabla TarjetasUsuario: {Message}", ex.Message);
                return false;
            }
        }
    }

    public class RecargaSaldoViewModel
    {
        public int IdTarjeta { get; set; }
        public decimal Monto { get; set; }
    }
}