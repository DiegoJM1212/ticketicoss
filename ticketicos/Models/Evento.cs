using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ticketicos.Models
{
    public class Evento
    {
        [Key]  // Aunque no es necesario por convención, agregarlo para ser explícito
        [Column("id_evento")]
        public int Id { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [Column("fecha")]
        public DateTime Fecha { get; set; }

        [Column("lugar")]
        public string LugarNombre { get; set; } = string.Empty;

        [Column("imagen_url")]
        public string? Imagen { get; set; }


        [Column("id_lugar")]
        [ForeignKey("Lugar")]
        public int IdLugar { get; set; }

        public Lugar? Lugar { get; set; }

        [Column("tipo_evento")]
        public string Categoria { get; set; } = string.Empty;

        public string Estado { get; set; } = string.Empty; // Agregado desde `Eventos`

        // Relación con Preventa
        public List<Preventa> Preventas { get; set; } = new List<Preventa>();

        // Relación con Boleto
        public List<Boleto> Boletos { get; set; } = new List<Boleto>();

        // Imagen en URL en lugar de byte array
        [NotMapped]
        public string? ImagenUrl { get; set; }
    }

    public class Preventa
    {
        [Key]
        [Column("id_preventa")]
        public int IdPreventa { get; set; }

        [Column("id_evento")]
        public int IdEvento { get; set; }

        [Column("fecha_inicio")]
        public DateTime FechaInicio { get; set; }

        [Column("fecha_fin")]
        public DateTime FechaFin { get; set; }

        [Column("estado")]
        public string Estado { get; set; } = string.Empty;

        // Relación con Evento
        public Evento? Evento { get; set; }
    }

    public class Boleto
    {
        [Key]
        [Column("id_boleto")]
        public int IdBoleto { get; set; }  // Aseguramos que sea la clave primaria

        [Column("id_evento")]
        public int IdEvento { get; set; }

        [Column("categoria")]
        public string Categoria { get; set; } = string.Empty;

        [Column("precio")]
        public decimal Precio { get; set; }

        [Column("cantidad_disponible")]
        public int CantidadDisponible { get; set; }

        [Column("limite_compra_por_usuario")]
        public int LimiteCompraPorUsuario { get; set; }

        [Column("id_preventa")]
        public int? IdPreventa { get; set; }

        [Column("precio_preventa")]
        public decimal? PrecioPreventa { get; set; }

        [Column("cantidad_preventa")]
        public int? CantidadPreventa { get; set; }

        // Relación con Evento y Preventa
        public Evento? Evento { get; set; }
        public Preventa? Preventa { get; set; }
    }
}
