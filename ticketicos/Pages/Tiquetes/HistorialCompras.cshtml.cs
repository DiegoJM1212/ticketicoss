using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ticketicos.Models;
using ticketicos.Data;

namespace ticketicos.Pages.Tiquetes
{
    public class HistorialComprasModel : PageModel
    {
        private readonly VerEventoContext _context;

        public HistorialComprasModel(VerEventoContext context)
        {
            _context = context;
        }

        // Inicializamos la propiedad para evitar que sea null
        public List<Compra> Compras { get; set; } = new List<Compra>();

        public void OnGet()
        {
            // Consultar las compras del usuario, con los eventos asociados
            Compras = _context.Compras
                .Include(c => c.Evento)  // Incluir el evento relacionado
                .ThenInclude(e => e.Lugar) // Incluir lugar del evento si lo necesitas
                .OrderByDescending(c => c.FechaCompra) // Ordenar por fecha de compra (más reciente primero)
                .ToList();
        }
    }
}

