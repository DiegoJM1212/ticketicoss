using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ticketicos.Data;
using ticketicos.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace ticketicos.Pages
{
    public class ConfirmacionCompraModel : PageModel
    {
        private readonly VerEventoContext _context;

        public ConfirmacionCompraModel(VerEventoContext context)
        {
            _context = context;
        }

        public PagoEntrada Pago { get; set; }
        public List<Compra> Compras { get; set; }

        public async Task<IActionResult> OnGetAsync(int idPago)
        {
            Pago = await _context.PagosEntradas.FirstOrDefaultAsync(p => p.IdPago == idPago);

            if (Pago == null)
            {
                return NotFound();
            }

            Compras = await _context.Compras
                .Where(c => c.IdPago == idPago)
                .Include(c => c.Evento)
                .ToListAsync();

            return Page();
        }
    }
}
