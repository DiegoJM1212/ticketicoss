using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ticketicos.Data;
using ticketicos.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ticketicos.Pages.Cliente
{
    public class ComprarBoletosModel : PageModel
    {
        private readonly VerEventoContext _context;

        public Evento? Evento { get; set; }
        public List<Bloque> Bloques { get; set; } = new List<Bloque>();

        [BindProperty]
        public int Cantidad { get; set; } = 1;

        [BindProperty]
        public int BloqueId { get; set; }

        [BindProperty]
        public List<string> AsientosSeleccionados { get; set; } = new List<string>();

        [BindProperty]
        public decimal Subtotal { get; set; }

        [BindProperty]
        public decimal Impuesto { get; set; }

        [BindProperty]
        public decimal Servicio { get; set; }

        [BindProperty]
        public decimal Total { get; set; }

        [BindProperty]
        public string PromoCode { get; set; } = string.Empty;

        [BindProperty]
        public string CardOwner { get; set; } = string.Empty;

        [BindProperty]
        public string CardNumber { get; set; } = string.Empty;

        [BindProperty]
        public string CardExpiry { get; set; } = string.Empty;

        [BindProperty]
        public string CardCvv { get; set; } = string.Empty;

        public ComprarBoletosModel(VerEventoContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Evento = await _context.Eventos
                .Include(e => e.Lugar)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (Evento == null)
            {
                return NotFound("Evento no encontrado.");
            }

            if (Evento.Lugar == null)
            {
                return NotFound("Lugar no definido para el evento.");
            }

            Bloques = await CargarBloquesAsync(Evento.Lugar.IdLugar);
            return Page();
        }

        public async Task<JsonResult> OnGetCargarAsientosAsync(int bloqueId, int eventId)
        {
            try
            {
                if (bloqueId <= 0 || eventId <= 0)
                {
                    return new JsonResult(new { error = "ID de bloque o evento no válido" });
                }

                var evento = await _context.Eventos
                    .Include(e => e.Lugar)
                    .FirstOrDefaultAsync(e => e.Id == eventId);

                if (evento == null)
                {
                    return new JsonResult(new { error = "Evento no encontrado" });
                }

                var asientos = await _context.Asientos
                    .Where(a => a.IdBloque == bloqueId && a.EstaDisponible)
                    .Select(a => new
                    {
                        idAsiento = a.IdAsiento,
                        numeroAsiento = a.NumeroAsiento ?? string.Empty,
                        esVip = a.EsVip,
                        estaDisponible = a.EstaDisponible
                    })
                    .ToListAsync();

                return new JsonResult(asientos);
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostComprarAsync(int eventoId)
        {
            try
            {
                Evento = await _context.Eventos
                    .Include(e => e.Lugar)
                    .FirstOrDefaultAsync(e => e.Id == eventoId);

                if (Evento == null)
                {
                    return NotFound("Evento no encontrado");
                }

                // Obtener asientos seleccionados del formulario
                string asientosStr = Request.Form["AsientosSeleccionados"].FirstOrDefault() ?? string.Empty;
                if (!string.IsNullOrEmpty(asientosStr))
                {
                    AsientosSeleccionados = asientosStr.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                // Validar que se hayan seleccionado asientos
                if (!AsientosSeleccionados.Any())
                {
                    ModelState.AddModelError("", "Por favor, selecciona al menos un asiento.");
                    if (Evento.Lugar != null)
                    {
                        Bloques = await CargarBloquesAsync(Evento.Lugar.IdLugar);
                    }
                    return Page();
                }

                // Obtener ID del bloque
                if (int.TryParse(Request.Form["BloqueId"].FirstOrDefault(), out int bloqueIdValue))
                {
                    BloqueId = bloqueIdValue;
                }
                else
                {
                    ModelState.AddModelError("", "Por favor, selecciona una zona.");
                    if (Evento.Lugar != null)
                    {
                        Bloques = await CargarBloquesAsync(Evento.Lugar.IdLugar);
                    }
                    return Page();
                }

                // Obtener valores de precio
                if (decimal.TryParse(Request.Form["Subtotal"].FirstOrDefault(), out decimal subtotalValue))
                {
                    Subtotal = subtotalValue;
                }

                if (decimal.TryParse(Request.Form["Impuesto"].FirstOrDefault(), out decimal impuestoValue))
                {
                    Impuesto = impuestoValue;
                }

                if (decimal.TryParse(Request.Form["Servicio"].FirstOrDefault(), out decimal servicioValue))
                {
                    Servicio = servicioValue;
                }

                if (decimal.TryParse(Request.Form["Total"].FirstOrDefault(), out decimal totalValue))
                {
                    Total = totalValue;
                }

                // Obtener código promocional
                PromoCode = Request.Form["PromoCode"].FirstOrDefault() ?? string.Empty;

                // Guardar datos en TempData para la siguiente página
                TempData["EventoId"] = eventoId;
                TempData["BloqueId"] = BloqueId;
                TempData["AsientosSeleccionados"] = string.Join(",", AsientosSeleccionados);
                TempData["Subtotal"] = Subtotal;
                TempData["Impuesto"] = Impuesto;
                TempData["Servicio"] = Servicio;
                TempData["Total"] = Total;
                TempData["PromoCode"] = PromoCode;

                return RedirectToPage("/Cliente/PagoEntradas");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error al procesar la compra: {ex.Message}");
                if (Evento?.Lugar != null)
                {
                    Bloques = await CargarBloquesAsync(Evento.Lugar.IdLugar);
                }
                return Page();
            }
        }

        private async Task<List<Bloque>> CargarBloquesAsync(int idLugar)
        {
            switch (idLugar)
            {
                case 1:
                    return await _context.BloqueEstadioNacional
                        .Where(b => b.habilitada)
                        .Select(b => new Bloque
                        {
                            Id = b.id_bloqueEN,
                            NombreBloque = b.nombre_bloque ?? string.Empty,
                            CapacidadAsientos = b.capacidad_asientos,
                            Precio = b.precio
                        })
                        .ToListAsync();
                case 4:
                    return await _context.BloqueTEATROMS
                        .Where(b => b.habilitada)
                        .Select(b => new Bloque
                        {
                            Id = b.id_bloqueTEATROMS,
                            NombreBloque = b.nombre_bloque ?? string.Empty,
                            CapacidadAsientos = b.capacidad_asientos,
                            Precio = b.precio
                        })
                        .ToListAsync();
                case 2:
                    return await _context.BloqueParqueViva
                        .Where(b => b.habilitada)
                        .Select(b => new Bloque
                        {
                            Id = b.id_bloque,
                            NombreBloque = b.nombre_bloque ?? string.Empty,
                            CapacidadAsientos = b.capacidad_asientos,
                            Precio = b.precio
                        })
                        .ToListAsync();
                case 3:
                    return await _context.BloqueCEP
                        .Where(b => b.habilitada)
                        .Select(b => new Bloque
                        {
                            Id = b.id_bloque,
                            NombreBloque = b.nombre_bloque ?? string.Empty,
                            CapacidadAsientos = b.capacidad_asientos,
                            Precio = b.precio
                        })
                        .ToListAsync();
                default:
                    return new List<Bloque>();
            }
        }
    }

    public class Bloque
    {
        public int Id { get; set; }
        public string NombreBloque { get; set; } = string.Empty;
        public int CapacidadAsientos { get; set; }
        public decimal Precio { get; set; }
    }
}
