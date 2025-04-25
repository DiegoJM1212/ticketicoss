using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ticketicos.Models
{
    [Table("Bloques")]
    public class Bloque
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id_bloque")]
        public int Id { get; set; }

        [Column("id_lugar")]
        public int IdLugar { get; set; }

        [Column("nombre_bloque")]
        [Required]
        public string NombreBloque { get; set; }

        [Column("capacidad_asientos")]
        public int CapacidadAsientos { get; set; }

        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; }

        [Column("habilitada")]
        public bool Habilitada { get; set; }

        [Column("precio")]
        public decimal Precio { get; set; }

        // Propiedad de navegación (opcional)
        public ICollection<Asiento> Asientos { get; set; }
    }
}
