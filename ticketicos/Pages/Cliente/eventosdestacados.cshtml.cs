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
    public class EventosDestacadosModel : PageModel
    {
        private readonly ILogger<EventosDestacadosModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public EventosDestacadosModel(ILogger<EventosDestacadosModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        public List<EventoViewModel> Eventos { get; set; } = new List<EventoViewModel>();

        public async Task OnGetAsync()
        {
            try
            {
                await CargarEventosDestacadosAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar los eventos destacados");
                Eventos = new List<EventoViewModel>(); // Asegurar que Eventos no sea null
            }
        }

        private async Task CargarEventosDestacadosAsync()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"
                    SELECT e.id_evento, e.nombre, e.descripcion, e.fecha, e.lugar, e.imagen_url
                    FROM Eventos e
                    INNER JOIN EventosDestacados ed ON e.id_evento = ed.id_evento
                    WHERE e.estado = 'activo' AND ed.activo = 1
                    ORDER BY e.fecha ASC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Eventos.Add(new EventoViewModel
                            {
                                IdEvento = reader.GetInt32(0),
                                Nombre = reader.GetString(1),
                                Descripcion = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Fecha = reader.GetDateTime(3),
                                Lugar = reader.GetString(4),
                                ImagenUrl = reader.IsDBNull(5) ? "/images/evento-default.jpg" : reader.GetString(5),
                                Hora = reader.GetDateTime(3).ToString("HH:mm")
                            });
                        }
                    }
                }
            }
        }
    }

    public class EventoViewModel
    {
        public int IdEvento { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Lugar { get; set; } = string.Empty;
        public string ImagenUrl { get; set; } = string.Empty;
        public string Hora { get; set; } = string.Empty;
    }
}