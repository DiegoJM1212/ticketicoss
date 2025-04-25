using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ticketicos.Pages.Admin
{
    public class GestionRolesModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public GestionRolesModel(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        public List<RolViewModel> Roles { get; set; } = new List<RolViewModel>();
        public List<EmpleadoViewModel> Empleados { get; set; } = new List<EmpleadoViewModel>();
        public List<PermisoViewModel> TodosLosPermisos { get; set; } = new List<PermisoViewModel>();

        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Verificar si el usuario es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");
                if (!userId.HasValue || userType != "admin")
                {
                    return RedirectToPage("/Cliente/Login");
                }

                // Cargar roles, empleados y permisos
                await CargarRolesAsync();
                await CargarEmpleadosAsync(SearchTerm);
                await CargarTodosLosPermisosAsync();
                return Page();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error al cargar los datos: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnGetObtenerPermisosRolAsync(int rolId)
        {
            try
            {
                List<int> permisosIds = new List<int>();
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT id_permiso
                        FROM RolesPermisos
                        WHERE id_rol = @RolId";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@RolId", rolId);
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                permisosIds.Add(reader.GetInt32(0));
                            }
                        }
                    }
                }
                return new JsonResult(permisosIds);
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostCrearRolAsync(string nombreRol, List<int> permisos)
        {
            try
            {
                // Verificar si el usuario es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");
                if (!userId.HasValue || userType != "admin")
                {
                    return RedirectToPage("/Cliente/Login");
                }

                // Verificar si el nombre del rol ya existe
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string checkQuery = "SELECT COUNT(*) FROM Roles WHERE nombre_rol = @Nombre";
                    using (SqlCommand command = new SqlCommand(checkQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Nombre", nombreRol);
                        int count = (int)await command.ExecuteScalarAsync();
                        if (count > 0)
                        {
                            TempData["ErrorMessage"] = $"El rol '{nombreRol}' ya existe.";
                            await CargarRolesAsync();
                            await CargarEmpleadosAsync(SearchTerm);
                            await CargarTodosLosPermisosAsync();
                            return Page();
                        }
                    }

                    // Crear el nuevo rol
                    int rolId;
                    string insertRolQuery = @"
                        INSERT INTO Roles (nombre_rol, descripcion)
                        VALUES (@Nombre, @Descripcion);
                        SELECT SCOPE_IDENTITY();";
                    using (SqlCommand command = new SqlCommand(insertRolQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Nombre", nombreRol);
                        command.Parameters.AddWithValue("@Descripcion", $"Rol {nombreRol} creado el {DateTime.Now}");
                        rolId = Convert.ToInt32(await command.ExecuteScalarAsync());
                    }

                    // Asignar permisos al rol
                    if (permisos != null && permisos.Count > 0)
                    {
                        foreach (var permisoId in permisos)
                        {
                            string insertPermisoQuery = @"
                                INSERT INTO RolesPermisos (id_rol, id_permiso)
                                VALUES (@RolId, @PermisoId)";
                            using (SqlCommand command = new SqlCommand(insertPermisoQuery, connection))
                            {
                                command.Parameters.AddWithValue("@RolId", rolId);
                                command.Parameters.AddWithValue("@PermisoId", permisoId);
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }

                TempData["SuccessMessage"] = $"Rol '{nombreRol}' creado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error al crear el rol: {ex.Message}";
            }

            await CargarRolesAsync();
            await CargarEmpleadosAsync(SearchTerm);
            await CargarTodosLosPermisosAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostActualizarRolAsync(int rolId, string nombreRol, List<int> permisos)
        {
            try
            {
                // Verificar si el usuario es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");
                if (!userId.HasValue || userType != "admin")
                {
                    return RedirectToPage("/Cliente/Login");
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Verificar si el nombre del rol ya existe (excepto para el rol actual)
                    string checkQuery = "SELECT COUNT(*) FROM Roles WHERE nombre_rol = @Nombre AND id_rol != @RolId";
                    using (SqlCommand command = new SqlCommand(checkQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Nombre", nombreRol);
                        command.Parameters.AddWithValue("@RolId", rolId);
                        int count = (int)await command.ExecuteScalarAsync();
                        if (count > 0)
                        {
                            TempData["ErrorMessage"] = $"El rol '{nombreRol}' ya existe.";
                            await CargarRolesAsync();
                            await CargarEmpleadosAsync(SearchTerm);
                            await CargarTodosLosPermisosAsync();
                            return Page();
                        }
                    }

                    // Actualizar el nombre del rol
                    string updateRolQuery = @"
                        UPDATE Roles 
                        SET nombre_rol = @Nombre
                        WHERE id_rol = @RolId";
                    using (SqlCommand command = new SqlCommand(updateRolQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Nombre", nombreRol);
                        command.Parameters.AddWithValue("@RolId", rolId);
                        await command.ExecuteNonQueryAsync();
                    }

                    // Eliminar permisos actuales
                    string deletePermisosQuery = @"
                        DELETE FROM RolesPermisos
                        WHERE id_rol = @RolId";
                    using (SqlCommand command = new SqlCommand(deletePermisosQuery, connection))
                    {
                        command.Parameters.AddWithValue("@RolId", rolId);
                        await command.ExecuteNonQueryAsync();
                    }

                    // Asignar nuevos permisos
                    if (permisos != null && permisos.Count > 0)
                    {
                        foreach (var permisoId in permisos)
                        {
                            string insertPermisoQuery = @"
                                INSERT INTO RolesPermisos (id_rol, id_permiso)
                                VALUES (@RolId, @PermisoId)";
                            using (SqlCommand command = new SqlCommand(insertPermisoQuery, connection))
                            {
                                command.Parameters.AddWithValue("@RolId", rolId);
                                command.Parameters.AddWithValue("@PermisoId", permisoId);
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }

                TempData["SuccessMessage"] = $"Rol '{nombreRol}' actualizado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error al actualizar el rol: {ex.Message}";
            }

            await CargarRolesAsync();
            await CargarEmpleadosAsync(SearchTerm);
            await CargarTodosLosPermisosAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostEliminarRolAsync(string nombre)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Eliminar asignaciones de usuarios al rol
                    string deleteUsuariosRolesQuery = @"
                        DELETE FROM UsuariosRoles
                        WHERE id_rol = (SELECT id_rol FROM Roles WHERE nombre_rol = @Nombre)";
                    using (SqlCommand command = new SqlCommand(deleteUsuariosRolesQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Nombre", nombre);
                        await command.ExecuteNonQueryAsync();
                    }

                    // Eliminar permisos asociados al rol
                    string deleteRolesPermisosQuery = @"
                        DELETE FROM RolesPermisos
                        WHERE id_rol = (SELECT id_rol FROM Roles WHERE nombre_rol = @Nombre)";
                    using (SqlCommand command = new SqlCommand(deleteRolesPermisosQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Nombre", nombre);
                        await command.ExecuteNonQueryAsync();
                    }

                    // Eliminar el rol
                    string deleteRolQuery = @"
                        DELETE FROM Roles
                        WHERE nombre_rol = @Nombre";
                    using (SqlCommand command = new SqlCommand(deleteRolQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Nombre", nombre);
                        await command.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = $"Rol '{nombre}' eliminado correctamente.";
                await CargarRolesAsync();
                await CargarEmpleadosAsync(SearchTerm);
                await CargarTodosLosPermisosAsync();
                return Page();
            }
            catch
            {
                TempData["ErrorMessage"] = $"Error al eliminar el rol '{nombre}'.";
                await CargarRolesAsync();
                await CargarEmpleadosAsync(SearchTerm);
                await CargarTodosLosPermisosAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostCambiarRolAsync(int idUsuario, string nuevoRol)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Eliminar roles actuales del usuario
                    string deleteQuery = @"
                        DELETE FROM UsuariosRoles
                        WHERE id_usuario = @IdUsuario";
                    using (SqlCommand command = new SqlCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@IdUsuario", idUsuario);
                        await command.ExecuteNonQueryAsync();
                    }

                    // Asignar el nuevo rol
                    if (!string.IsNullOrEmpty(nuevoRol))
                    {
                        string insertQuery = @"
                            INSERT INTO UsuariosRoles (id_usuario, id_rol)
                            VALUES (@IdUsuario, (SELECT id_rol FROM Roles WHERE nombre_rol = @NombreRol))";
                        using (SqlCommand command = new SqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@IdUsuario", idUsuario);
                            command.Parameters.AddWithValue("@NombreRol", nuevoRol);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Actualizar tipo_usuario en Usuarios
                        string updateQuery = @"
                            UPDATE Usuarios
                            SET tipo_usuario = CASE 
                                WHEN @NombreRol IN ('gestor_eventos', 'gestor_ventas', 'patrocinador', 'manager') THEN 'admin'
                                ELSE 'empleado'
                            END
                            WHERE id_usuario = @IdUsuario";
                        using (SqlCommand command = new SqlCommand(updateQuery, connection))
                        {
                            command.Parameters.AddWithValue("@IdUsuario", idUsuario);
                            command.Parameters.AddWithValue("@NombreRol", nuevoRol);
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }

                TempData["SuccessMessage"] = "Rol asignado correctamente.";
            }
            catch
            {
                TempData["ErrorMessage"] = "Error al asignar el rol.";
            }

            await CargarRolesAsync();
            await CargarEmpleadosAsync(SearchTerm);
            await CargarTodosLosPermisosAsync();
            return Page();
        }

        private async Task CargarRolesAsync()
        {
            Roles.Clear();
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"
                    SELECT r.id_rol, r.nombre_rol, 
                           COUNT(DISTINCT ur.id_usuario) as usuarios,
                           STRING_AGG(p.nombre_permiso, ', ') as permisos
                    FROM Roles r
                    LEFT JOIN UsuariosRoles ur ON r.id_rol = ur.id_rol
                    LEFT JOIN RolesPermisos rp ON r.id_rol = rp.id_rol
                    LEFT JOIN Permisos p ON rp.id_permiso = p.id_permiso
                    GROUP BY r.id_rol, r.nombre_rol";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Roles.Add(new RolViewModel
                            {
                                Id = reader.GetInt32(0),
                                Nombre = reader.GetString(1),
                                Usuarios = reader.GetInt32(2),
                                Permisos = reader.IsDBNull(3) ? new List<string>() : reader.GetString(3).Split(", ", StringSplitOptions.None).ToList()
                            });
                        }
                    }
                }
            }
        }

        private async Task CargarEmpleadosAsync(string searchTerm)
        {
            Empleados.Clear();
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"
                    SELECT u.id_usuario, u.nombre + ' ' + u.apellidos AS nombre, r.nombre_rol
                    FROM Usuarios u
                    LEFT JOIN UsuariosRoles ur ON u.id_usuario = ur.id_usuario
                    LEFT JOIN Roles r ON ur.id_rol = r.id_rol
                    WHERE u.tipo_usuario IN ('empleado', 'admin')
                    AND (@SearchTerm = '' OR u.nombre LIKE '%' + @SearchTerm + '%' OR u.apellidos LIKE '%' + @SearchTerm + '%')";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SearchTerm", searchTerm ?? string.Empty);
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Empleados.Add(new EmpleadoViewModel
                            {
                                IdUsuario = reader.GetInt32(0),
                                Nombre = reader.GetString(1),
                                RolActual = reader.IsDBNull(2) ? "Sin rol" : reader.GetString(2)
                            });
                        }
                    }
                }
            }
        }

        private async Task CargarTodosLosPermisosAsync()
        {
            TodosLosPermisos.Clear();
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = "SELECT id_permiso, nombre_permiso FROM Permisos";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            TodosLosPermisos.Add(new PermisoViewModel
                            {
                                Id = reader.GetInt32(0),
                                Nombre = reader.GetString(1)
                            });
                        }
                    }
                }
            }
        }
    }

    public class RolViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Usuarios { get; set; }
        public List<string> Permisos { get; set; } = new List<string>();
    }

    public class EmpleadoViewModel
    {
        public int IdUsuario { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string RolActual { get; set; } = string.Empty;
    }

    public class PermisoViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }
}