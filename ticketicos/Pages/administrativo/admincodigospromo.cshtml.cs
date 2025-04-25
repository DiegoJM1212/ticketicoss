using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace ticketicos.Pages.Administrativo
{
    public class AdminCodigosPromoModel : PageModel
    {
        private readonly ILogger<AdminCodigosPromoModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public AdminCodigosPromoModel(ILogger<AdminCodigosPromoModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        public List<CodigoPromoViewModel> CodigosPromo { get; set; } = new List<CodigoPromoViewModel>();

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
                    _logger.LogWarning("Intento de acceso no autorizado a la administración de códigos promocionales");
                    return RedirectToPage("/Cliente/Login");
                }

                await CargarCodigosPromocionalesAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar la página de administración de códigos promocionales");
                MensajeError = "Ocurrió un error al cargar los códigos promocionales.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostCrearCodigoAsync(string codigo, decimal descuento, DateTime fechaExpiracion, string estado)
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para crear código promocional");
                    return Unauthorized();
                }

                // Validaciones
                if (string.IsNullOrWhiteSpace(codigo))
                {
                    return new JsonResult(new { success = false, message = "El código promocional no puede estar vacío." });
                }

                if (descuento <= 0)
                {
                    return new JsonResult(new { success = false, message = "El descuento debe ser mayor que cero." });
                }

                if (fechaExpiracion < DateTime.Today)
                {
                    return new JsonResult(new { success = false, message = "La fecha de expiración no puede ser anterior a hoy." });
                }

                if (estado != "activo" && estado != "expirado")
                {
                    return new JsonResult(new { success = false, message = "El estado debe ser 'activo' o 'expirado'." });
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Verificar si el código ya existe
                    string checkQuery = @"
                        SELECT COUNT(*) FROM CodigosPromocionales 
                        WHERE codigo = @Codigo";

                    using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@Codigo", codigo);
                        int count = (int)(await checkCommand.ExecuteScalarAsync() ?? 0);

                        if (count > 0)
                        {
                            return new JsonResult(new { success = false, message = "El código promocional ya existe." });
                        }
                    }

                    // Insertar el nuevo código promocional
                    string insertQuery = @"
                        INSERT INTO CodigosPromocionales (codigo, descuento, fecha_expiracion, estado)
                        VALUES (@Codigo, @Descuento, @FechaExpiracion, @Estado);
                        SELECT SCOPE_IDENTITY();";

                    using (SqlCommand insertCommand = new SqlCommand(insertQuery, connection))
                    {
                        insertCommand.Parameters.AddWithValue("@Codigo", codigo);
                        insertCommand.Parameters.AddWithValue("@Descuento", descuento);
                        insertCommand.Parameters.AddWithValue("@FechaExpiracion", fechaExpiracion);
                        insertCommand.Parameters.AddWithValue("@Estado", estado);

                        object? result = await insertCommand.ExecuteScalarAsync();
                        int newId = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;

                        if (newId > 0)
                        {
                            _logger.LogInformation("Código promocional creado con ID {Id}", newId);
                            return new JsonResult(new { success = true, message = "Código promocional creado exitosamente." });
                        }
                        else
                        {
                            return new JsonResult(new { success = false, message = "No se pudo crear el código promocional." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear código promocional");
                return new JsonResult(new { success = false, message = "Ocurrió un error al crear el código promocional: " + ex.Message });
            }
        }

        public async Task<IActionResult> OnPostActualizarCodigoAsync(int id, string codigo)
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para actualizar código promocional");
                    return new JsonResult(new { success = false, message = "Acceso no autorizado." });
                }

                if (string.IsNullOrWhiteSpace(codigo))
                {
                    return new JsonResult(new { success = false, message = "El código promocional no puede estar vacío." });
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Verificar si el código ya existe
                    string checkQuery = @"
                        SELECT COUNT(*) FROM CodigosPromocionales 
                        WHERE codigo = @Codigo AND id_codigo != @Id";

                    using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@Codigo", codigo);
                        checkCommand.Parameters.AddWithValue("@Id", id);
                        int count = (int)(await checkCommand.ExecuteScalarAsync() ?? 0);

                        if (count > 0)
                        {
                            return new JsonResult(new { success = false, message = "El código promocional ya existe." });
                        }
                    }

                    // Actualizar el código promocional
                    string updateQuery = @"
                        UPDATE CodigosPromocionales 
                        SET codigo = @Codigo 
                        WHERE id_codigo = @Id";

                    using (SqlCommand updateCommand = new SqlCommand(updateQuery, connection))
                    {
                        updateCommand.Parameters.AddWithValue("@Codigo", codigo);
                        updateCommand.Parameters.AddWithValue("@Id", id);
                        int rowsAffected = await updateCommand.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            _logger.LogInformation("Código promocional actualizado con ID {Id}", id);
                            return new JsonResult(new { success = true, message = "Código promocional actualizado exitosamente." });
                        }
                        else
                        {
                            return new JsonResult(new { success = false, message = "No se encontró el código promocional." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar código promocional con ID {Id}", id);
                return new JsonResult(new { success = false, message = "Ocurrió un error al actualizar el código promocional: " + ex.Message });
            }
        }

        public async Task<IActionResult> OnPostEliminarCodigoAsync(int id)
        {
            try
            {
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para eliminar código promocional");
                    return new JsonResult(new { success = false, message = "Acceso no autorizado." });
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Verificar si el código está siendo utilizado en alguna compra
                    string checkQuery = @"
                        SELECT COUNT(*) FROM Compras 
                        WHERE id_codigo_promocional = @Id";

                    using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@Id", id);
                        int count = (int)(await checkCommand.ExecuteScalarAsync() ?? 0);

                        if (count > 0)
                        {
                            return new JsonResult(new { success = false, message = "No se puede eliminar el código promocional porque está siendo utilizado en compras." });
                        }
                    }

                    // Eliminar el código promocional
                    string deleteQuery = @"
                        DELETE FROM CodigosPromocionales 
                        WHERE id_codigo = @Id";

                    using (SqlCommand deleteCommand = new SqlCommand(deleteQuery, connection))
                    {
                        deleteCommand.Parameters.AddWithValue("@Id", id);
                        int rowsAffected = await deleteCommand.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            _logger.LogInformation("Código promocional eliminado con ID {Id}", id);
                            return new JsonResult(new { success = true, message = "Código promocional eliminado exitosamente." });
                        }
                        else
                        {
                            return new JsonResult(new { success = false, message = "No se encontró el código promocional." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar código promocional con ID {Id}", id);
                return new JsonResult(new { success = false, message = "Ocurrió un error al eliminar el código promocional: " + ex.Message });
            }
        }

        private async Task CargarCodigosPromocionalesAsync()
        {
            CodigosPromo.Clear();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT id_codigo, codigo, descuento, fecha_expiracion, estado
                    FROM CodigosPromocionales
                    ORDER BY fecha_expiracion DESC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int id = reader.GetInt32(0);
                            string codigo = reader.GetString(1);
                            decimal descuento = reader.GetDecimal(2);
                            DateTime fechaExpiracion = reader.GetDateTime(3);
                            string estado = reader.GetString(4);

                            // Calcular días restantes
                            int diasRestantes = (fechaExpiracion - DateTime.Now).Days;
                            string validez = diasRestantes > 0
                                ? $"Válido hasta: {fechaExpiracion:dd/MM/yyyy} ({diasRestantes} días restantes)"
                                : "Expirado";

                            // Crear el modelo de vista para el código promocional
                            var codigoPromo = new CodigoPromoViewModel
                            {
                                Id = id,
                                Codigo = codigo,
                                PorcentajeDescuento = (int)descuento, // Asumiendo que descuento es un porcentaje
                                Monto = descuento,
                                Validez = validez,
                                Descripcion = ObtenerDescripcionPorCodigo(codigo),
                                Estado = estado
                            };

                            CodigosPromo.Add(codigoPromo);
                        }
                    }
                }
            }
        }

        private string ObtenerDescripcionPorCodigo(string codigo)
        {
            if (codigo.Contains("2x1", StringComparison.OrdinalIgnoreCase))
            {
                return "Válido para cualquier evento";
            }
            else if (codigo.Contains("DESCUENTO", StringComparison.OrdinalIgnoreCase))
            {
                return "Válido para conciertos";
            }
            else if (codigo.Contains("DEPORTE", StringComparison.OrdinalIgnoreCase))
            {
                return "Válido para eventos deportivos";
            }

            return string.Empty;
        }
    }

    public class CodigoPromoViewModel
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public int PorcentajeDescuento { get; set; }
        public decimal Monto { get; set; }
        public string Validez { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
    }
}