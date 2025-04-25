using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ticketicos.Models;
using ticketicos.Data;

public class VerEventoModel : PageModel
{
    private readonly VerEventoContext _context;

    public VerEventoModel(VerEventoContext context)
    {
        _context = context;
    }

    public Evento? Evento { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        // Consultamos el evento con el id proporcionado
        Evento = await _context.Eventos
                               .Include(e => e.Lugar) // Si Lugar es una propiedad relacionada
                               .FirstOrDefaultAsync(e => e.Id == id);

        // Si no se encuentra el evento, devolvemos una respuesta 404
        if (Evento == null)
        {
            return NotFound();
        }

        // Si encontramos el evento, lo mostramos en la vista
        return Page();
    }
}
