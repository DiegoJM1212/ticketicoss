using Microsoft.EntityFrameworkCore;
using ticketicos.Models;

namespace ticketicos.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Tablas de la base de datos
        public DbSet<Evento> Eventos { get; set; }
        public DbSet<Preventa> Preventas { get; set; }
        public DbSet<Boleto> Boletos { get; set; }
        public DbSet<TarjetaUsuario> TarjetasUsuario { get; set; }
        public DbSet<Compra> Compras { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de Evento
            modelBuilder.Entity<Evento>().ToTable("Eventos");
            modelBuilder.Entity<Evento>().HasKey(e => e.Id);
            modelBuilder.Entity<Evento>().Property(e => e.Id).HasColumnName("id_evento");
            modelBuilder.Entity<Evento>().Property(e => e.Nombre).HasColumnName("nombre");
            modelBuilder.Entity<Evento>().Property(e => e.Descripcion).HasColumnName("descripcion");
            modelBuilder.Entity<Evento>().Property(e => e.Fecha).HasColumnName("fecha");
            modelBuilder.Entity<Evento>().Property(e => e.LugarNombre).HasColumnName("lugar");
            modelBuilder.Entity<Evento>().Property(e => e.Categoria).HasColumnName("tipo_evento");
            modelBuilder.Entity<Evento>().Property(e => e.Imagen).HasColumnName("imagen_url");

            // Configuración de Preventa
            modelBuilder.Entity<Preventa>().ToTable("Preventa");
            modelBuilder.Entity<Preventa>().HasKey(p => p.IdPreventa);
            modelBuilder.Entity<Preventa>().Property(p => p.IdPreventa).HasColumnName("id_preventa");
            modelBuilder.Entity<Preventa>().Property(p => p.IdEvento).HasColumnName("id_evento");
            modelBuilder.Entity<Preventa>().Property(p => p.FechaInicio).HasColumnName("fecha_inicio");
            modelBuilder.Entity<Preventa>().Property(p => p.FechaFin).HasColumnName("fecha_fin");
            modelBuilder.Entity<Preventa>().Property(p => p.Estado).HasColumnName("estado");

            // Relación Evento-Preventa (Evento tiene una lista de preventas)
            modelBuilder.Entity<Preventa>()
                .HasOne(p => p.Evento)
                .WithMany(e => e.Preventas)
                .HasForeignKey(p => p.IdEvento);

            // Configuración de Boleto
            modelBuilder.Entity<Boleto>().ToTable("Boletos");
            modelBuilder.Entity<Boleto>().HasKey(b => b.IdBoleto);
            modelBuilder.Entity<Boleto>().Property(b => b.IdBoleto).HasColumnName("id_boleto");
            modelBuilder.Entity<Boleto>().Property(b => b.IdEvento).HasColumnName("id_evento");
            modelBuilder.Entity<Boleto>().Property(b => b.Categoria).HasColumnName("categoria");
            modelBuilder.Entity<Boleto>().Property(b => b.Precio).HasColumnName("precio");
            modelBuilder.Entity<Boleto>().Property(b => b.CantidadDisponible).HasColumnName("cantidad_disponible");
            modelBuilder.Entity<Boleto>().Property(b => b.LimiteCompraPorUsuario).HasColumnName("limite_compra_por_usuario");
            modelBuilder.Entity<Boleto>().Property(b => b.IdPreventa).HasColumnName("id_preventa");
            modelBuilder.Entity<Boleto>().Property(b => b.PrecioPreventa).HasColumnName("precio_preventa");
            modelBuilder.Entity<Boleto>().Property(b => b.CantidadPreventa).HasColumnName("cantidad_preventa");

            // Relación Boleto-Evento
            modelBuilder.Entity<Boleto>()
                .HasOne(b => b.Evento)
                .WithMany(e => e.Boletos)
                .HasForeignKey(b => b.IdEvento);

            // Relación Boleto-Preventa
            modelBuilder.Entity<Boleto>()
                .HasOne(b => b.Preventa)
                .WithMany()
                .HasForeignKey(b => b.IdPreventa);

            // Configuración de Compras
            modelBuilder.Entity<Compra>().ToTable("Compras");
            modelBuilder.Entity<Compra>().HasKey(c => c.IdCompra);
            modelBuilder.Entity<Compra>().Property(c => c.IdCompra).HasColumnName("id_compra");
            modelBuilder.Entity<Compra>().Property(c => c.IdUsuario).HasColumnName("id_usuario");
            modelBuilder.Entity<Compra>().Property(c => c.IdEvento).HasColumnName("id_evento");
            modelBuilder.Entity<Compra>().Property(c => c.Total).HasColumnName("total");
            modelBuilder.Entity<Compra>().Property(c => c.FechaCompra).HasColumnName("fecha_compra");

            // Relación Compra-Evento
            modelBuilder.Entity<Compra>()
                .HasOne(c => c.Evento)
                .WithMany()
                .HasForeignKey(c => c.IdEvento);
        }
    }
}
