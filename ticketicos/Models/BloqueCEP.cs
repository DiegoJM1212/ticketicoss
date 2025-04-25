using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ticketicos.Models
{
    [Table("BloqueCEP")]
    public class BloqueCEP
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id_bloqueCEP")]
        public int id_bloque { get; set; }

        [Column("id_lugar")]
        public int id_lugar { get; set; } // Para Centro de Eventos Pedregal, id_lugar = 3

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
