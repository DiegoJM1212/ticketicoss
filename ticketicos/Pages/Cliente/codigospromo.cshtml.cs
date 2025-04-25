using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Pages.Cliente
{
    public class CodigosPromoModel : PageModel
    {
        private readonly ILogger<CodigosPromoModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public CodigosPromoModel(ILogger<CodigosPromoModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        public List<PromocionViewModel> Promociones { get; set; } = new List<PromocionViewModel>();

        public async Task OnGetAsync()
        {
            try
            {
                await CargarCodigosPromocionalesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar los códigos promocionales");
            }
        }

        private async Task CargarCodigosPromocionalesAsync()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Verificar si existen las columnas precio, descripcion y validez
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

                // Agregar columnas faltantes
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

                // Consulta para obtener códigos promocionales activos
                string query = @"
                    SELECT id_codigo, codigo, descuento, fecha_expiracion, estado, 
                           ISNULL(precio, 5000) as precio, 
                           ISNULL(descripcion, 'Código promocional') as descripcion, 
                           ISNULL(validez, 'Todos los eventos') as validez
                    FROM CodigosPromocionales
                    WHERE estado = 'activo' AND fecha_expiracion >= GETDATE()
                    ORDER BY fecha_expiracion ASC";

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
                            decimal precio = reader.GetDecimal(5);
                            string descripcion = reader.GetString(6);
                            string validez = reader.GetString(7);

                            int diasRestantes = (fechaExpiracion - DateTime.Now).Days;

                            var promocion = new PromocionViewModel
                            {
                                Id = id,
                                Descuento = $"{descuento:C0} de descuento",
                                Validez = $"Válido hasta: {fechaExpiracion.ToString("dd/MM/yyyy")} ({diasRestantes} días restantes)",
                                Precio = $"Precio: {precio:C0}",
                                CodigoDescripcion = descripcion,
                                ValidoPara = validez,
                                EsPreventa = diasRestantes <= 7
                            };

                            Promociones.Add(promocion);
                        }
                    }
                }
            }
        }
    }

    public class PromocionViewModel
    {
        public int Id { get; set; }
        public string Descuento { get; set; } = string.Empty;
        public string Validez { get; set; } = string.Empty;
        public string Precio { get; set; } = string.Empty;
        public string CodigoDescripcion { get; set; } = string.Empty;
        public string ValidoPara { get; set; } = string.Empty;
        public bool EsPreventa { get; set; }
    }
}