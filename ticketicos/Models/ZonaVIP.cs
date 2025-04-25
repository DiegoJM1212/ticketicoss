using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ticketicos.Models
{
    [Table("ZonasVIP")]
    public class ZonaVIP
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id_zona_vip")]
        public int IdZonaVIP { get; set; }

        [Column("id_lugar")]
        public int IdLugar { get; set; }

        [Column("nombre_zona")]
        public string NombreZona { get; set; }

        [Column("capacidad_asientos")]
        public int CapacidadAsientos { get; set; }

        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; }

        [Column("habilitada")]
        public bool Habilitada { get; set; }

        // Relación con Lugar (opcional)
        [ForeignKey("IdLugar")]
        public Lugar Lugar { get; set; }
    }
}
