using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ticketicos.Pages.Admin
{
    public class AdminLugaresModel : PageModel
    {
        private readonly ILogger<AdminLugaresModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public AdminLugaresModel(ILogger<AdminLugaresModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            Lugares = new List<LugarViewModel>();
            Bloques = new List<BloqueViewModel>();
            Asientos = new List<AsientoViewModel>();
            ZonasComunes = new List<ZonaComunViewModel>();
            ZonasVIP = new List<ZonaVipViewModel>();
        }

        public List<LugarViewModel> Lugares { get; set; }
        public List<BloqueViewModel> Bloques { get; set; }
        public List<AsientoViewModel> Asientos { get; set; }
        public List<ZonaComunViewModel> ZonasComunes { get; set; }
        public List<ZonaVipViewModel> ZonasVIP { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            // Verificar si el usuario está autenticado y es administrador
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? userType = HttpContext.Session.GetString("UserType");

            if (!userId.HasValue || userType != "admin")
            {
                _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                return RedirectToPage("/Cliente/Login");
            }

            try
            {
                await CargarDatosAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos de gestión de edificaciones");
                ErrorMessage = "Error al cargar los datos. Intenta nuevamente.";
                return Page();
            }
        }

        // Métodos para Lugares
        public async Task<IActionResult> OnPostCrearLugarAsync(string NombreLugar, string DireccionLugar, string MapaInteractivo)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "INSERT INTO Lugares (nombre, direccion, mapa_interactivo) VALUES (@Nombre, @Direccion, @Mapa)";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Nombre", NombreLugar);
                        command.Parameters.AddWithValue("@Direccion", DireccionLugar);
                        command.Parameters.AddWithValue("@Mapa", (object)MapaInteractivo ?? DBNull.Value);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Lugar creado exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear lugar");
                ErrorMessage = "Error al crear el lugar. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostActualizarLugarAsync(int IdLugar, string NombreLugar, string DireccionLugar, string MapaInteractivo)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "UPDATE Lugares SET nombre = @Nombre, direccion = @Direccion, mapa_interactivo = @Mapa WHERE id_lugar = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", IdLugar);
                        command.Parameters.AddWithValue("@Nombre", NombreLugar);
                        command.Parameters.AddWithValue("@Direccion", DireccionLugar);
                        command.Parameters.AddWithValue("@Mapa", (object)MapaInteractivo ?? DBNull.Value);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Lugar actualizado exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar lugar");
                ErrorMessage = "Error al actualizar el lugar. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostEliminarLugarAsync(int IdLugar)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "DELETE FROM Lugares WHERE id_lugar = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", IdLugar);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Lugar eliminado exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar lugar");
                ErrorMessage = "Error al eliminar el lugar. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        // Métodos para Bloques
        public async Task<IActionResult> OnPostCrearBloqueAsync(int IdLugarBloque, string NombreBloque, decimal PrecioBloque, bool HabilitadoBloque)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "INSERT INTO Bloques (id_lugar, nombre_bloque, precio, habilitada) VALUES (@IdLugar, @Nombre, @Precio, @Habilitada)";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdLugar", IdLugarBloque);
                        command.Parameters.AddWithValue("@Nombre", NombreBloque);
                        command.Parameters.AddWithValue("@Precio", PrecioBloque);
                        command.Parameters.AddWithValue("@Habilitada", HabilitadoBloque);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Bloque creado exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear bloque");
                ErrorMessage = "Error al crear el bloque. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostActualizarBloqueAsync(int IdBloque, int IdLugarBloque, string NombreBloque, decimal PrecioBloque, bool HabilitadoBloque)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "UPDATE Bloques SET id_lugar = @IdLugar, nombre_bloque = @Nombre, precio = @Precio, habilitada = @Habilitada WHERE id_bloque = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", IdBloque);
                        command.Parameters.AddWithValue("@IdLugar", IdLugarBloque);
                        command.Parameters.AddWithValue("@Nombre", NombreBloque);
                        command.Parameters.AddWithValue("@Precio", PrecioBloque);
                        command.Parameters.AddWithValue("@Habilitada", HabilitadoBloque);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Bloque actualizado exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar bloque");
                ErrorMessage = "Error al actualizar el bloque. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostEliminarBloqueAsync(int IdBloque)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "DELETE FROM Bloques WHERE id_bloque = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", IdBloque);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Bloque eliminado exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar bloque");
                ErrorMessage = "Error al eliminar el bloque. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        // Métodos para Asientos
        public async Task<IActionResult> OnPostCrearAsientoAsync(int IdBloqueAsiento, string NumeroAsiento, bool EsVipAsiento, bool HabilitadoAsiento)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "INSERT INTO Asientos (id_bloque, numero_asiento, es_vip, esta_disponible, habilitada) VALUES (@IdBloque, @Numero, @EsVip, 1, @Habilitada)";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdBloque", IdBloqueAsiento);
                        command.Parameters.AddWithValue("@Numero", NumeroAsiento);
                        command.Parameters.AddWithValue("@EsVip", EsVipAsiento);
                        command.Parameters.AddWithValue("@Habilitada", HabilitadoAsiento);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Asiento creado exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear asiento");
                ErrorMessage = "Error al crear el asiento. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostActualizarAsientoAsync(int IdAsiento, int IdBloqueAsiento, string NumeroAsiento, bool EsVipAsiento, bool HabilitadoAsiento)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "UPDATE Asientos SET id_bloque = @IdBloque, numero_asiento = @Numero, es_vip = @EsVip, habilitada = @Habilitada WHERE id_asiento = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", IdAsiento);
                        command.Parameters.AddWithValue("@IdBloque", IdBloqueAsiento);
                        command.Parameters.AddWithValue("@Numero", NumeroAsiento);
                        command.Parameters.AddWithValue("@EsVip", EsVipAsiento);
                        command.Parameters.AddWithValue("@Habilitada", HabilitadoAsiento);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Asiento actualizado exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar asiento");
                ErrorMessage = "Error al actualizar el asiento. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostEliminarAsientoAsync(int IdAsiento)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "DELETE FROM Asientos WHERE id_asiento = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", IdAsiento);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Asiento eliminado exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar asiento");
                ErrorMessage = "Error al eliminar el asiento. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        // Métodos para Zonas Comunes
        public async Task<IActionResult> OnPostCrearZonaComunAsync(int IdLugarZonaComun, string NombreZonaComun, int CapacidadZonaComun)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "INSERT INTO ZonasComunes (id_lugar, nombre_zona, capacidad) VALUES (@IdLugar, @Nombre, @Capacidad)";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdLugar", IdLugarZonaComun);
                        command.Parameters.AddWithValue("@Nombre", NombreZonaComun);
                        command.Parameters.AddWithValue("@Capacidad", CapacidadZonaComun);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Zona común creada exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear zona común");
                ErrorMessage = "Error al crear la zona común. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostActualizarZonaComunAsync(int IdZonaComun, int IdLugarZonaComun, string NombreZonaComun, int CapacidadZonaComun)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "UPDATE ZonasComunes SET id_lugar = @IdLugar, nombre_zona = @Nombre, capacidad = @Capacidad WHERE id_zona_comun = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", IdZonaComun);
                        command.Parameters.AddWithValue("@IdLugar", IdLugarZonaComun);
                        command.Parameters.AddWithValue("@Nombre", NombreZonaComun);
                        command.Parameters.AddWithValue("@Capacidad", CapacidadZonaComun);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Zona común actualizada exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar zona común");
                ErrorMessage = "Error al actualizar la zona común. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostEliminarZonaComunAsync(int IdZonaComun)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "DELETE FROM ZonasComunes WHERE id_zona_comun = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", IdZonaComun);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Zona común eliminada exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar zona común");
                ErrorMessage = "Error al eliminar la zona común. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        // Métodos para Zonas Preferenciales
        public async Task<IActionResult> OnPostCrearZonaPreferencialAsync(int IdLugarZonaVip, string NombreZonaVip, int CapacidadZonaVip, decimal PrecioZonaVip)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "INSERT INTO ZonasVIP (id_lugar, nombre_zona, capacidad, precio) VALUES (@IdLugar, @Nombre, @Capacidad, @Precio)";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdLugar", IdLugarZonaVip);
                        command.Parameters.AddWithValue("@Nombre", NombreZonaVip);
                        command.Parameters.AddWithValue("@Capacidad", CapacidadZonaVip);
                        command.Parameters.AddWithValue("@Precio", PrecioZonaVip);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Zona preferencial creada exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear zona preferencial");
                ErrorMessage = "Error al crear la zona preferencial. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostActualizarZonaVipAsync(int IdZonaVip, int IdLugarZonaVip, string NombreZonaVip, int CapacidadZonaVip, decimal PrecioZonaVip)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "UPDATE ZonasVIP SET id_lugar = @IdLugar, nombre_zona = @Nombre, capacidad = @Capacidad, precio = @Precio WHERE id_zona_vip = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", IdZonaVip);
                        command.Parameters.AddWithValue("@IdLugar", IdLugarZonaVip);
                        command.Parameters.AddWithValue("@Nombre", NombreZonaVip);
                        command.Parameters.AddWithValue("@Capacidad", CapacidadZonaVip);
                        command.Parameters.AddWithValue("@Precio", PrecioZonaVip);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Zona preferencial actualizada exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar zona preferencial");
                ErrorMessage = "Error al actualizar la zona preferencial. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostEliminarZonaVipAsync(int IdZonaVip)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de edificaciones");
                    return RedirectToPage("/Cliente/Login");
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "DELETE FROM ZonasVIP WHERE id_zona_vip = @Id";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", IdZonaVip);
                        await command.ExecuteNonQueryAsync();
                    }
                }
                SuccessMessage = "Zona preferencial eliminada exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar zona preferencial");
                ErrorMessage = "Error al eliminar la zona preferencial. Intenta nuevamente.";
            }
            await CargarDatosAsync();
            return Page();
        }

        // Modificar el método CargarDatosAsync para corregir las consultas SQL
        private async Task CargarDatosAsync()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Cargar Lugares - Corregir la consulta para usar solo columnas existentes
                    Lugares.Clear();
                    var lugarQuery = "SELECT id_lugar, nombre, direccion, mapa_interactivo FROM Lugares";
                    using (var command = new SqlCommand(lugarQuery, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Lugares.Add(new LugarViewModel
                                {
                                    Id = reader.GetInt32(0),
                                    Nombre = reader.GetString(1),
                                    Direccion = reader.GetString(2),
                                    MapaInteractivo = reader.IsDBNull(3) ? null : reader.GetString(3)
                                });
                            }
                        }
                    }

                    // Cargar Bloques - Asegurarse de que la tabla Bloques tenga la columna precio
                    Bloques.Clear();
                    var bloqueQuery = @"
                SELECT b.id_bloque, b.id_lugar, l.nombre AS lugar_nombre, b.nombre_bloque, b.precio, b.habilitada
                FROM Bloques b
                JOIN Lugares l ON b.id_lugar = l.id_lugar";
                    using (var command = new SqlCommand(bloqueQuery, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Bloques.Add(new BloqueViewModel
                                {
                                    Id = reader.GetInt32(0),
                                    IdLugar = reader.GetInt32(1),
                                    LugarNombre = reader.GetString(2),
                                    Nombre = reader.GetString(3),
                                    Precio = reader.GetDecimal(4),
                                    Habilitado = reader.GetBoolean(5)
                                });
                            }
                        }
                    }

                    // Cargar Asientos
                    Asientos.Clear();
                    var asientoQuery = @"
                SELECT a.id_asiento, a.id_bloque, b.nombre_bloque, a.numero_asiento, a.es_vip, a.habilitada
                FROM Asientos a
                JOIN Bloques b ON a.id_bloque = b.id_bloque";
                    using (var command = new SqlCommand(asientoQuery, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Asientos.Add(new AsientoViewModel
                                {
                                    Id = reader.GetInt32(0),
                                    IdBloque = reader.GetInt32(1),
                                    BloqueNombre = reader.GetString(2),
                                    Numero = reader.GetString(3),
                                    EsVip = reader.GetBoolean(4),
                                    Habilitado = reader.GetBoolean(5)
                                });
                            }
                        }
                    }

                    // Cargar Zonas Comunes - Corregir la consulta para usar solo columnas existentes
                    ZonasComunes.Clear();
                    var zonaComunQuery = @"
                SELECT zc.id_zona_comun, zc.id_lugar, l.nombre AS lugar_nombre, zc.nombre_zona, zc.capacidad
                FROM ZonasComunes zc
                JOIN Lugares l ON zc.id_lugar = l.id_lugar";
                    using (var command = new SqlCommand(zonaComunQuery, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                ZonasComunes.Add(new ZonaComunViewModel
                                {
                                    Id = reader.GetInt32(0),
                                    IdLugar = reader.GetInt32(1),
                                    LugarNombre = reader.GetString(2),
                                    Nombre = reader.GetString(3),
                                    Capacidad = reader.GetInt32(4)
                                });
                            }
                        }
                    }

                    // Cargar Zonas VIP - Asegurarse de que la tabla ZonasVIP tenga la columna precio
                    ZonasVIP.Clear();
                    var zonaVipQuery = @"
                SELECT zv.id_zona_vip, zv.id_lugar, l.nombre AS lugar_nombre, zv.nombre_zona, zv.capacidad, zv.precio
                FROM ZonasVIP zv
                JOIN Lugares l ON zv.id_lugar = l.id_lugar";
                    using (var command = new SqlCommand(zonaVipQuery, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                ZonasVIP.Add(new ZonaVipViewModel
                                {
                                    Id = reader.GetInt32(0),
                                    IdLugar = reader.GetInt32(1),
                                    LugarNombre = reader.GetString(2),
                                    Nombre = reader.GetString(3),
                                    Capacidad = reader.GetInt32(4),
                                    Precio = reader.GetDecimal(5)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos de gestión de edificaciones");
                ErrorMessage = "Error al cargar los datos. Intenta nuevamente.";
                throw; // Re-lanzar la excepción para que se maneje en el método que llama
            }
        }
    }

    public class LugarViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string? MapaInteractivo { get; set; }
    }

    public class BloqueViewModel
    {
        public int Id { get; set; }
        public int IdLugar { get; set; }
        public string LugarNombre { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public bool Habilitado { get; set; }
    }

    public class AsientoViewModel
    {
        public int Id { get; set; }
        public int IdBloque { get; set; }
        public string BloqueNombre { get; set; } = string.Empty;
        public string Numero { get; set; } = string.Empty;
        public bool EsVip { get; set; }
        public bool Habilitado { get; set; }
    }

    public class ZonaComunViewModel
    {
        public int Id { get; set; }
        public int IdLugar { get; set; }
        public string LugarNombre { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public int Capacidad { get; set; }
    }

    public class ZonaVipViewModel
    {
        public int Id { get; set; }
        public int IdLugar { get; set; }
        public string LugarNombre { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public int Capacidad { get; set; }
        public decimal Precio { get; set; }
    }
}
