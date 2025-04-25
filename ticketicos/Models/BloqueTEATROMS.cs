using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ticketicos.Models
{
    [Table("BloqueTEATROMS")]
    public class BloqueTEATROMS
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id_bloqueTMS")]
        public int id_bloqueTEATROMS { get; set; }

        [Column("id_lugar")]
        public int id_lugar { get; set; } // Para Teatro Melico Salazar, id_lugar = 4

        [Column("nombre_bloque")]
        public string nombre_bloque { get; set; }

        [Column("capacidad_asientos")]
        public int capacidad_asientos { get; set; }

        [Column("fecha_creacion")]
        public DateTime fecha_creacion { get; set; }

        [Column("habilitada")]
        public bool habilitada { get; set; }

        [Column("precio")]
        public decimal precio { get; set; }
    }
}
