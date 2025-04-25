using Microsoft.EntityFrameworkCore;
using ticketicos.Models;

namespace ticketicos.Data
{
    public class TicketContext : DbContext
    {
        public TicketContext(DbContextOptions<TicketContext> options) : base(options)
        {
        }

        public DbSet<Evento> Eventos { get; set; }
    }
}
