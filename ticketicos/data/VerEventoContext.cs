using Microsoft.EntityFrameworkCore;
using ticketicos.Models;

namespace ticketicos.Data
{
    public class VerEventoContext : DbContext
    {
        public VerEventoContext(DbContextOptions<VerEventoContext> options)
            : base(options)
        {
        }

        public DbSet<Evento> Eventos { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<CodigoProfesional> CodigosProfecionales { get; set; }

        // Tablas de compra de entradas
        public DbSet<PagoEntrada> PagosEntradas { get; set; }
        public DbSet<Compra> Compras { get; set; }
        public DbSet<Bloque> Bloques { get; set; }
        public DbSet<Asiento> Asientos { get; set; }

        // Agregamos zonas y lugares
        public DbSet<ZonaVIP> ZonasVIP { get; set; }
        public DbSet<ZonaComun> ZonasComunes { get; set; }
        public DbSet<Lugar> Lugares { get; set; }

        // DbSet para bloques específicos
        public DbSet<BloqueEstadioNacional> BloqueEstadioNacional { get; set; }
        public DbSet<BloqueTEATROMS> BloqueTEATROMS { get; set; }
        public DbSet<BloqueParqueViva> BloqueParqueViva { get; set; }
        public DbSet<BloqueCEP> BloqueCEP { get; set; }

        // DbSet para asientos específicos
        public DbSet<AsientoEstadioNacional> AsientosEstadioNacional { get; set; }
        public DbSet<AsientoTEATROMelicoSalazar> AsientosTEATROMelicoSalazar { get; set; }
        public DbSet<AsientoParqueViva> AsientosParqueViva { get; set; }
        public DbSet<AsientoCEP> AsientosCEP { get; set; }
    }
}
