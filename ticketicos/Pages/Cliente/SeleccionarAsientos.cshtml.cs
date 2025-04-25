using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ticketicos.Pages.Cliente
{
    public class SeleccionarAsientosModel : PageModel
    {
        private readonly ILogger<SeleccionarAsientosModel> _logger;
        private readonly string _connectionString;

        public SeleccionarAsientosModel(ILogger<SeleccionarAsientosModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            Evento = new EventoViewModel();
            Bloques = new List<BloqueViewModel>();
            AsientosSeleccionados = new List<string>();
            FilasAsientos = new Dictionary<string, List<AsientoViewModel>>();
        }

        [BindProperty(SupportsGet = true)]
        public int EventoId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int BloqueId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Cantidad { get; set; } = 1;

        [BindProperty]
        public string Asientos { get; set; } = string.Empty;

        public EventoViewModel Evento { get; set; }
        public List<BloqueViewModel> Bloques { get; set; }
        public BloqueViewModel? BloqueSeleccionado { get; set; }
        public List<string> AsientosSeleccionados { get; set; }
        public Dictionary<string, List<AsientoViewModel>> FilasAsientos { get; set; }
        public decimal Subtotal { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string MapaInteractivo { get; set; } = string.Empty;
        public int CantidadEntradas => Cantidad > 0 ? Cantidad : 1;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Iniciando OnGetAsync para EventoId: {EventoId}", EventoId);

                if (EventoId <= 0)
                {
                    _logger.LogWarning("EventoId inválido: {EventoId}", EventoId);
                    ErrorMessage = "Evento no válido.";
                    return RedirectToPage("/Cliente/Eventos");
                }

                await CargarEventoAsync();
                _logger.LogInformation("Evento cargado: IdLugar={IdLugar}, Nombre={Nombre}", Evento.IdLugar, Evento.Nombre);

                if (Evento == null || Evento.Id == 0 || !Evento.IdLugar.HasValue)
                {
                    _logger.LogWarning("Evento ID {EventoId} no encontrado o sin lugar", EventoId);
                    ErrorMessage = "Evento no encontrado.";
                    return NotFound();
                }

                await CargarBloquesDirectoAsync();
                _logger.LogInformation("Bloques cargados: {Count}", Bloques.Count);

                if (!Bloques.Any())
                {
                    _logger.LogWarning("No se encontraron bloques para EventoId {EventoId}, IdLugar {IdLugar}", EventoId, Evento.IdLugar);
                    ErrorMessage = "No hay zonas disponibles para este evento.";
                    return Page();
                }

                if (BloqueId > 0)
                {
                    BloqueSeleccionado = Bloques.FirstOrDefault(b => b.Id == BloqueId);
                    if (BloqueSeleccionado == null)
                    {
                        _logger.LogWarning("Bloque ID {BloqueId} no encontrado para EventoId {EventoId}", BloqueId, EventoId);
                        ErrorMessage = "Zona no válida.";
                        return Page();
                    }

                    await CargarAsientosDirectoAsync();
                    _logger.LogInformation("Asientos cargados: {FilasCount} filas", FilasAsientos.Count);
                    Subtotal = BloqueSeleccionado.Precio * CantidadEntradas;
                }

                if (!string.IsNullOrEmpty(Asientos))
                {
                    AsientosSeleccionados = Asientos.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (AsientosSeleccionados.Count > CantidadEntradas)
                    {
                        AsientosSeleccionados = AsientosSeleccionados.Take(CantidadEntradas).ToList();
                        ErrorMessage = $"Solo puedes seleccionar hasta {CantidadEntradas} asiento(s).";
                    }
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en OnGetAsync para EventoId {EventoId}", EventoId);
                ErrorMessage = "Error al cargar la página.";
                return Page();
            }
        }

        private async Task CargarEventoAsync()
        {
            try
            {
                _logger.LogInformation("Cargando evento ID: {EventoId}", EventoId);
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = @"
                        SELECT e.id_evento, e.nombre, e.fecha, e.lugar, e.id_lugar, e.imagen_url, 
                               l.nombre AS lugar_nombre, l.mapa_interactivo,
                               COALESCE(p.limite_boletos_por_usuario, 2) AS limite_boletos
                        FROM Eventos e
                        LEFT JOIN Lugares l ON e.id_lugar = l.id_lugar
                        LEFT JOIN Preventa p ON e.id_evento = p.id_evento
                        WHERE e.id_evento = @EventoId AND e.estado = 'activo'";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@EventoId", EventoId);
                        _logger.LogInformation("Ejecutando consulta evento: {Query}", query);

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                Evento = new EventoViewModel
                                {
                                    Id = reader.GetInt32(0),
                                    Nombre = reader.GetString(1),
                                    Fecha = reader.GetDateTime(2),
                                    Hora = reader.GetDateTime(2).ToString("HH:mm"),
                                    Lugar = reader.GetString(3),
                                    IdLugar = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                                    ImagenUrl = reader.IsDBNull(5) ? "/images/evento-default.jpg" : reader.GetString(5),
                                    LugarNombre = reader.IsDBNull(6) ? "Desconocido" : reader.GetString(6),
                                    LimiteBoletos = reader.GetInt32(8)
                                };
                                if (!reader.IsDBNull(7))
                                {
                                    MapaInteractivo = reader.GetString(7);
                                }
                                _logger.LogInformation("Evento cargado: ID={Id}, Nombre={Nombre}, IdLugar={IdLugar}, LimiteBoletos={LimiteBoletos}",
                                    Evento.Id, Evento.Nombre, Evento.IdLugar, Evento.LimiteBoletos);
                            }
                            else
                            {
                                _logger.LogWarning("Evento ID {EventoId} no encontrado o no activo", EventoId);
                                ErrorMessage = "Evento no encontrado o no activo.";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar evento ID {EventoId}", EventoId);
                ErrorMessage = "Error al cargar el evento.";
            }
        }

        private async Task CargarBloquesDirectoAsync()
        {
            try
            {
                if (Evento == null || !Evento.IdLugar.HasValue)
                {
                    _logger.LogError("Evento nulo o sin IdLugar para EventoId {EventoId}", EventoId);
                    ErrorMessage = "Error: No se pudo determinar el lugar.";
                    return;
                }

                int idLugar = Evento.IdLugar.Value;
                _logger.LogInformation("Cargando bloques para EventoId {EventoId}, IdLugar {IdLugar}", EventoId, idLugar);

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "";
                    string tableName = "";
                    string idColumnName = "";

                    switch (idLugar)
                    {
                        case 1: // Estadio Nacional
                        case 5:
                            tableName = "BloqueEstadioNacional";
                            idColumnName = "id_bloqueEN";
                            break;
                        case 4: // Parque Viva
                        case 8:
                        case 11:
                            tableName = "BloqueParqueViva";
                            idColumnName = "id_bloquePV";
                            break;
                        case 2: // Teatro Popular Melico Salazar
                        case 6:
                            tableName = "BloqueTEATROMS";
                            idColumnName = "id_bloqueTMS";
                            break;
                        case 3: // Centro de Eventos Pedregal
                        case 7:
                        case 9:
                        case 10:
                            tableName = "BloqueCEP";
                            idColumnName = "id_bloqueCEP";
                            break;
                        default:
                            _logger.LogWarning("IdLugar {IdLugar} no soportado para EventoId {EventoId}", idLugar, EventoId);
                            ErrorMessage = "Lugar no soportado.";
                            return;
                    }

                    query = $@"SELECT {idColumnName} AS id, nombre_bloque AS nombre, precio, capacidad_asientos, 
                              CASE WHEN nombre_bloque LIKE '%VIP%' THEN 1 ELSE 0 END AS es_vip
                              FROM {tableName}
                              WHERE id_lugar = @IdLugar AND habilitada = 1";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdLugar", idLugar);
                        _logger.LogInformation("Ejecutando consulta bloques: {Query} con IdLugar={IdLugar}", query, idLugar);

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            Bloques.Clear();
                            while (await reader.ReadAsync())
                            {
                                var bloque = new BloqueViewModel
                                {
                                    Id = reader.GetInt32(0),
                                    Nombre = reader.GetString(1),
                                    Precio = reader.GetDecimal(2),
                                    CantidadAsientos = reader.GetInt32(3),
                                    EsVip = reader.GetBoolean(4)
                                };
                                Bloques.Add(bloque);
                                _logger.LogInformation("Bloque cargado: ID={Id}, Nombre={Nombre}, Precio={Precio}", bloque.Id, bloque.Nombre, bloque.Precio);
                            }
                            _logger.LogInformation("Total bloques cargados: {Count} desde {TableName}", Bloques.Count, tableName);
                        }
                    }
                }

                if (!Bloques.Any())
                {
                    _logger.LogWarning("No se encontraron bloques para IdLugar {IdLugar}, EventoId {EventoId}", idLugar, EventoId);
                    ErrorMessage = "No hay zonas disponibles.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar bloques para IdLugar {IdLugar}, EventoId {EventoId}", Evento?.IdLugar, EventoId);
                ErrorMessage = "Error al cargar las zonas.";
            }
        }

        private async Task CargarAsientosDirectoAsync()
        {
            try
            {
                if (BloqueId <= 0 || Evento == null || !Evento.IdLugar.HasValue)
                {
                    _logger.LogError("Datos inválidos: BloqueId={BloqueId}, IdLugar={IdLugar}, EventoId={EventoId}", BloqueId, Evento?.IdLugar, EventoId);
                    ErrorMessage = "Error: Datos insuficientes para asientos.";
                    return;
                }

                int idLugar = Evento.IdLugar.Value;
                _logger.LogInformation("Cargando asientos para BloqueId {BloqueId}, IdLugar {IdLugar}, EventoId {EventoId}", BloqueId, idLugar, EventoId);

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "";
                    string tableName = "";

                    switch (idLugar)
                    {
                        case 1: // Estadio Nacional
                        case 5:
                            tableName = "AsientosEstadioNacional";
                            query = @"SELECT id_asiento, numero_asiento, es_vip, esta_disponible
                                      FROM AsientosEstadioNacional
                                      WHERE id_bloqueEN = @BloqueId AND habilitada = 1
                                      ORDER BY numero_asiento";
                            break;
                        case 4: // Parque Viva
                        case 8:
                        case 11:
                            tableName = "AsientosParqueViva";
                            query = @"SELECT id_asiento, numero_asiento, es_vip, esta_disponible
                                      FROM AsientosParqueViva
                                      WHERE id_bloquePV = @BloqueId AND habilitada = 1
                                      ORDER BY numero_asiento";
                            break;
                        case 2: // Teatro Popular Melico Salazar
                        case 6:
                            tableName = "AsientosTEATROMelicoSalazar";
                            query = @"SELECT id_asiento, numero_asiento, es_vip, esta_disponible
                                      FROM AsientosTEATROMelicoSalazar
                                      WHERE id_bloqueTMS = @BloqueId AND habilitada = 1
                                      ORDER BY numero_asiento";
                            break;
                        case 3: // Centro de Eventos Pedregal
                        case 7:
                        case 9:
                        case 10:
                            tableName = "AsientosCEP";
                            query = @"SELECT id_asiento, numero_asiento, es_vip, esta_disponible
                                      FROM AsientosCEP
                                      WHERE id_bloqueCEP = @BloqueId AND habilitada = 1
                                      ORDER BY numero_asiento";
                            break;
                        default:
                            _logger.LogWarning("IdLugar {IdLugar} no soportado para EventoId {EventoId}", idLugar, EventoId);
                            ErrorMessage = "Lugar no soportado para asientos.";
                            return;
                    }

                    List<AsientoViewModel> asientos = new List<AsientoViewModel>();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@BloqueId", BloqueId);
                        _logger.LogInformation("Ejecutando consulta asientos: {Query} con BloqueId={BloqueId}", query, BloqueId);

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                asientos.Add(new AsientoViewModel
                                {
                                    Id = reader.GetInt32(0),
                                    Numero = reader.GetString(1),
                                    EsVip = reader.GetBoolean(2),
                                    Disponible = reader.GetBoolean(3)
                                });
                            }
                            _logger.LogInformation("Asientos cargados: {Count} desde {TableName}", asientos.Count, tableName);
                        }
                    }

                    if (!asientos.Any())
                    {
                        _logger.LogWarning("No se encontraron asientos para BloqueId {BloqueId}, IdLugar {IdLugar}", BloqueId, idLugar);
                        ErrorMessage = "No hay asientos disponibles.";
                        return;
                    }

                    FilasAsientos = asientos
                        .GroupBy(a => a.Numero.Length > 0 ? a.Numero.Substring(0, 1) : "?")
                        .OrderBy(g => g.Key)
                        .ToDictionary(g => g.Key, g => g.OrderBy(a =>
                        {
                            string numPart = a.Numero.Substring(1);
                            return int.TryParse(numPart, out int num) ? num : 999;
                        }).ToList());

                    _logger.LogInformation("Asientos organizados en {FilasCount} filas", FilasAsientos.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar asientos para BloqueId {BloqueId}, EventoId {EventoId}", BloqueId, EventoId);
                ErrorMessage = "Error al cargar los asientos.";
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(Asientos))
                {
                    _logger.LogWarning("No se seleccionaron asientos para EventoId {EventoId}", EventoId);
                    ErrorMessage = $"Debes seleccionar {CantidadEntradas} asiento(s).";
                    await CargarEventoAsync();
                    await CargarBloquesDirectoAsync();
                    return Page();
                }

                AsientosSeleccionados = Asientos.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (AsientosSeleccionados.Count != CantidadEntradas)
                {
                    _logger.LogWarning("Asientos seleccionados ({Count}) no coinciden con cantidad ({CantidadEntradas})", AsientosSeleccionados.Count, CantidadEntradas);
                    ErrorMessage = $"Debes seleccionar exactamente {CantidadEntradas} asiento(s).";
                    await CargarEventoAsync();
                    await CargarBloquesDirectoAsync();
                    return Page();
                }

                // Aquí puedes agregar la verificación de disponibilidad si es necesario
                decimal precioPorAsiento = Bloques.FirstOrDefault(b => b.Id == BloqueId)?.Precio ?? 0;
                if (precioPorAsiento == 0)
                {
                    _logger.LogWarning("No se encontró precio para BloqueId {BloqueId}, EventoId {EventoId}", BloqueId, EventoId);
                    ErrorMessage = "Error al calcular el precio.";
                    await CargarEventoAsync();
                    await CargarBloquesDirectoAsync();
                    return Page();
                }

                decimal subtotal = precioPorAsiento * CantidadEntradas;
                decimal impuesto = subtotal * 0.13m;
                decimal servicio = subtotal * 0.05m;
                decimal total = subtotal + impuesto + servicio;

                string asientosString = string.Join(",", AsientosSeleccionados);
                _logger.LogInformation("Redirigiendo a PagoEntradas para EventoId {EventoId}, BloqueId {BloqueId}, Asientos {Asientos}", EventoId, BloqueId, asientosString);

                return RedirectToPage("/Cliente/PagoEntradas", new
                {
                    eventoId = EventoId,
                    bloqueId = BloqueId,
                    asientosSeleccionados = asientosString,
                    subtotal,
                    impuesto,
                    servicio,
                    total
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar selección para EventoId {EventoId}", EventoId);
                ErrorMessage = "Error al procesar la selección.";
                await CargarEventoAsync();
                await CargarBloquesDirectoAsync();
                return Page();
            }
        }
    }

    public class EventoViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Hora { get; set; } = string.Empty;
        public string Lugar { get; set; } = string.Empty;
        public string LugarNombre { get; set; } = string.Empty;
        public string ImagenUrl { get; set; } = string.Empty;
        public int? IdLugar { get; set; }
        public int LimiteBoletos { get; set; } = 2;
    }

    public class BloqueViewModel
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int CantidadAsientos { get; set; }
        public bool EsVip { get; set; }
    }

    public class AsientoViewModel
    {
        public int Id { get; set; }
        public string Numero { get; set; } = string.Empty;
        public bool EsVip { get; set; }
        public bool Disponible { get; set; }
    }
}