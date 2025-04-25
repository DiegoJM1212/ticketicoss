using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ticketicos.Models
{
    [Table("ZonasComunes")]
    public class ZonaComun
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id_zona_comun")]
        public int IdZonaComun { get; set; }

        [Column("id_lugar")]
        public int IdLugar { get; set; }

        [Column("nombre_zona")]
        public string NombreZona { get; set; }

        [Column("capacidad")]
        public int Capacidad { get; set; }

        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; }

        [Column("habilitada")]
        public bool Habilitada { get; set; }

        // Relación con Lugar (opcional)
        [ForeignKey("IdLugar")]
        public Lugar Lugar { get; set; }
    }
}
