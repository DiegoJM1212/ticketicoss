using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace ticketicos.Pages.Admin
{
    public class DashboardModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public DashboardModel(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        public int UsuariosActivos { get; set; }
        public int EventosActivos { get; set; }
        public decimal TotalComprasMes { get; set; }
        public int TotalBoletosVendidos { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? userType = HttpContext.Session.GetString("UserType");
            if (!userId.HasValue || userType != "admin")
            {
                return RedirectToPage("/Cliente/Login");
            }

            await CargarMetricasAsync();
            return Page();
        }

        private async Task CargarMetricasAsync()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Número de usuarios activos
                using (SqlCommand command = new SqlCommand(
                    "SELECT COUNT(*) FROM Usuarios WHERE estado = 'activo'", connection))
                {
                    UsuariosActivos = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
                }

                // Número de eventos activos
                using (SqlCommand command = new SqlCommand(
                    "SELECT COUNT(*) FROM Eventos WHERE estado = 'activo'", connection))
                {
                    EventosActivos = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
                }

                // Total de compras pagadas en el último mes (Compras + ComprasCodigos)
                using (SqlCommand command = new SqlCommand(@"
                    SELECT COALESCE(SUM(total), 0)
                    FROM (
                        SELECT total FROM Compras
                        WHERE estado = 'pagado'
                        AND fecha >= CAST(DATEADD(MONTH, -1, CAST(GETDATE() AS DATE)) AS DATE)
                        AND fecha <= CAST(GETDATE() AS DATE)
                        UNION ALL
                        SELECT total FROM ComprasCodigos
                        WHERE total > 0
                        AND fecha >= CAST(DATEADD(MONTH, -1, CAST(GETDATE() AS DATE)) AS DATE)
                        AND fecha <= CAST(GETDATE() AS DATE)
                    ) AS CombinedTotals", connection))
                {
                    TotalComprasMes = Convert.ToDecimal(await command.ExecuteScalarAsync() ?? 0);
                }

                // Total boletos vendidos último mes
                using (SqlCommand command = new SqlCommand(@"
                    SELECT COALESCE(SUM(dc.cantidad), 0)
                    FROM DetalleCompra dc
                    JOIN Compras c ON dc.id_compra = c.id_compra
                    WHERE c.estado = 'pagado'
                    AND c.fecha >= CAST(DATEADD(MONTH, -1, CAST(GETDATE() AS DATE)) AS DATE)
                    AND c.fecha <= CAST(GETDATE() AS DATE)", connection))
                {
                    TotalBoletosVendidos = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
                }
            }
        }
    }
}