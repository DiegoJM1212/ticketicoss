using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ticketicos.Models
{
    [Table("Lugares")]
    public class Lugar
    {
        [Key]
        [Column("id_lugar")]
        public int IdLugar { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; }

        [Column("direccion")]
        public string Direccion { get; set; }

        [Column("ciudad")]
        public string Ciudad { get; set; }

        [Column("capacidad")]
        public int Capacidad { get; set; }

        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; }

        [Column("mapa_interactivo")]
        public string? MapaInteractivo { get; set; }

        [Column("habilitada")]
        public bool Habilitada { get; set; }

        // Relación con eventos (opcional, si deseas navegar desde Lugar)
        public ICollection<Evento> Eventos { get; set; }
    }
}
