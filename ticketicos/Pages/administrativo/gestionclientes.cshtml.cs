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
    public class GestionClientesModel : PageModel
    {
        private readonly ILogger<GestionClientesModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public GestionClientesModel(ILogger<GestionClientesModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        public List<UsuarioViewModel> Usuarios { get; set; } = new List<UsuarioViewModel>();

        [TempData]
        public string? MensajeExito { get; set; }

        [TempData]
        public string? MensajeError { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Busqueda { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de usuarios");
                    return RedirectToPage("/Cliente/Login");
                }

                await CargarUsuariosAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar la página de gestión de usuarios");
                MensajeError = $"Ocurrió un error al cargar los usuarios: {ex.Message}";
                return Page();
            }
        }

        // Método para obtener el estado de un usuario (para el modal de edición)
        public async Task<IActionResult> OnGetObtenerEstadoUsuarioAsync(int id)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    return new JsonResult(new { error = "No autorizado" }) { StatusCode = 401 };
                }

                string estado = string.Empty;
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT estado FROM Usuarios WHERE id_usuario = @Id";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        var result = await command.ExecuteScalarAsync();
                        if (result != null)
                        {
                            estado = result.ToString();
                        }
                    }
                }

                return new JsonResult(new { estado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el estado del usuario");
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        // Modificar el método OnPostCambiarEstadoAsync para usar 'inactivo' en lugar de 'suspendido'
        public async Task<IActionResult> OnPostCambiarEstadoAsync(int id, string nuevoEstado)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para cambiar estado de usuario");
                    return RedirectToPage("/Cliente/Login");
                }

                // Normalizar el estado para asegurar que coincida exactamente con lo que espera la restricción CHECK
                string estadoNormalizado = nuevoEstado.Trim().ToLower();
                if (estadoNormalizado != "activo" && estadoNormalizado != "inactivo")
                {
                    MensajeError = "Estado no válido. Debe ser 'activo' o 'inactivo'.";
                    await CargarUsuariosAsync();
                    return Page();
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // No permitir cambiar el estado del propio usuario administrador
                    if (id == userId)
                    {
                        MensajeError = "No puedes cambiar tu propio estado.";
                        await CargarUsuariosAsync();
                        return Page();
                    }

                    // Verificar si el usuario existe
                    string checkQuery = "SELECT COUNT(*) FROM Usuarios WHERE id_usuario = @Id";
                    using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@Id", id);
                        int count = (int)await checkCommand.ExecuteScalarAsync();
                        if (count == 0)
                        {
                            MensajeError = "El usuario no existe.";
                            await CargarUsuariosAsync();
                            return Page();
                        }
                    }

                    try
                    {
                        // Actualizar el estado del usuario con el valor normalizado
                        string updateQuery = @"
                    UPDATE Usuarios 
                    SET estado = @Estado 
                    WHERE id_usuario = @Id";

                        using (SqlCommand command = new SqlCommand(updateQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Estado", estadoNormalizado);
                            command.Parameters.AddWithValue("@Id", id);
                            int rowsAffected = await command.ExecuteNonQueryAsync();

                            if (rowsAffected > 0)
                            {
                                // Registrar la acción en la tabla RegistroAcciones
                                await RegistrarAccionAsync(userId.Value, id, "cambio_estado",
                                    $"Cambio de estado de usuario a '{estadoNormalizado}'");

                                MensajeExito = $"Estado del usuario actualizado a '{estadoNormalizado}'.";
                            }
                            else
                            {
                                MensajeError = "No se pudo actualizar el estado del usuario.";
                            }
                        }
                    }
                    catch (SqlException sqlEx)
                    {
                        _logger.LogError(sqlEx, "Error de SQL al cambiar estado de usuario: {Message}", sqlEx.Message);
                        MensajeError = $"Error de base de datos: {sqlEx.Message}";
                    }
                }

                await CargarUsuariosAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al cambiar estado de usuario");
                MensajeError = $"Error inesperado: {ex.Message}";
                await CargarUsuariosAsync();
                return Page();
            }
        }

        // Modificar el método OnPostEditarUsuarioAsync para usar 'inactivo' en lugar de 'suspendido'
        public async Task<IActionResult> OnPostEditarUsuarioAsync(int id, string nombreCompleto, string correo, string estado)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para editar usuario");
                    return RedirectToPage("/Cliente/Login");
                }

                // Normalizar el estado para asegurar que coincida exactamente con lo que espera la restricción CHECK
                string estadoNormalizado = estado.Trim().ToLower();
                if (estadoNormalizado != "activo" && estadoNormalizado != "inactivo")
                {
                    MensajeError = "Estado no válido. Debe ser 'activo' o 'inactivo'.";
                    await CargarUsuariosAsync();
                    return Page();
                }

                // Validar datos básicos
                if (string.IsNullOrWhiteSpace(nombreCompleto) || string.IsNullOrWhiteSpace(correo))
                {
                    MensajeError = "El nombre y el correo son obligatorios.";
                    await CargarUsuariosAsync();
                    return Page();
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // No permitir cambiar el estado del propio usuario administrador
                    if (id == userId && estadoNormalizado != "activo")
                    {
                        MensajeError = "No puedes cambiar tu propio estado a inactivo.";
                        await CargarUsuariosAsync();
                        return Page();
                    }

                    try
                    {
                        // Separar el nombre completo en nombre y apellidos
                        string[] nombrePartes = nombreCompleto.Split(' ', 2);
                        string nombre = nombrePartes[0];
                        string apellidos = nombrePartes.Length > 1 ? nombrePartes[1] : "";

                        // Actualizar el usuario
                        string updateQuery = @"
            UPDATE Usuarios 
            SET nombre = @Nombre,
                apellidos = @Apellidos,
                correo = @Correo,
                estado = @Estado
            WHERE id_usuario = @Id";

                        using (SqlCommand command = new SqlCommand(updateQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Nombre", nombre);
                            command.Parameters.AddWithValue("@Apellidos", apellidos);
                            command.Parameters.AddWithValue("@Correo", correo);
                            command.Parameters.AddWithValue("@Estado", estadoNormalizado);
                            command.Parameters.AddWithValue("@Id", id);

                            int rowsAffected = await command.ExecuteNonQueryAsync();

                            if (rowsAffected > 0)
                            {
                                // Registrar la acción en la tabla RegistroAcciones
                                await RegistrarAccionAsync(userId.Value, id, "edicion_usuario",
                                    $"Actualización de datos de usuario");

                                MensajeExito = "Usuario actualizado correctamente.";
                            }
                            else
                            {
                                MensajeError = "No se pudo actualizar el usuario.";
                            }
                        }
                    }
                    catch (SqlException sqlEx)
                    {
                        _logger.LogError(sqlEx, "Error de SQL al editar usuario: {Message}", sqlEx.Message);

                        if (sqlEx.Message.Contains("CK__Usuarios__estado"))
                        {
                            MensajeError = "El estado debe ser exactamente 'activo' o 'inactivo'.";
                        }
                        else
                        {
                            MensajeError = $"Error de base de datos: {sqlEx.Message}";
                        }
                    }
                }

                await CargarUsuariosAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al editar usuario");
                MensajeError = $"Error inesperado: {ex.Message}";
                await CargarUsuariosAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostBuscarAsync()
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la búsqueda de usuarios");
                    return RedirectToPage("/Cliente/Login");
                }

                await CargarUsuariosAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar usuarios");
                MensajeError = $"Ocurrió un error al buscar usuarios: {ex.Message}";
                await CargarUsuariosAsync();
                return Page();
            }
        }

        private async Task CargarUsuariosAsync()
        {
            Usuarios.Clear();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT u.id_usuario, u.nombre, u.apellidos, u.correo, u.tipo_usuario, 
                           u.estado, u.ultima_fecha_acceso
                    FROM Usuarios u
                    WHERE (@Busqueda IS NULL OR 
                           u.nombre LIKE '%' + @Busqueda + '%' OR 
                           u.apellidos LIKE '%' + @Busqueda + '%' OR 
                           u.correo LIKE '%' + @Busqueda + '%')
                    ORDER BY u.ultima_fecha_acceso DESC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Busqueda", Busqueda ?? (object)DBNull.Value);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int id = reader.GetInt32(0);
                            string nombre = reader.GetString(1);
                            string apellidos = reader.GetString(2);
                            string correo = reader.GetString(3);
                            string tipoUsuario = reader.GetString(4);
                            string estado = reader.GetString(5);
                            DateTime? ultimoAcceso = reader.IsDBNull(6) ? null : reader.GetDateTime(6);

                            var usuario = new UsuarioViewModel
                            {
                                Id = id,
                                NombreCompleto = $"{nombre} {apellidos}",
                                Correo = correo,
                                TipoUsuario = tipoUsuario,
                                Estado = estado,
                                UltimoAcceso = ultimoAcceso
                            };

                            Usuarios.Add(usuario);
                        }
                    }
                }
            }
        }

        private async Task RegistrarAccionAsync(int idAdmin, int idUsuarioAfectado, string tipoAccion, string detalleAccion)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        INSERT INTO RegistroAcciones (id_usuario_admin, id_usuario_afectado, tipo_accion, detalle_accion, fecha_accion)
                        VALUES (@IdAdmin, @IdUsuarioAfectado, @TipoAccion, @DetalleAccion, @FechaAccion)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdAdmin", idAdmin);
                        command.Parameters.AddWithValue("@IdUsuarioAfectado", idUsuarioAfectado);
                        command.Parameters.AddWithValue("@TipoAccion", tipoAccion);
                        command.Parameters.AddWithValue("@DetalleAccion", detalleAccion);
                        command.Parameters.AddWithValue("@FechaAccion", DateTime.Now);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar acción administrativa");
                // No lanzamos la excepción para que no afecte al flujo principal
            }
        }
    }

    public class UsuarioViewModel
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string TipoUsuario { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateTime? UltimoAcceso { get; set; }
    }
}
