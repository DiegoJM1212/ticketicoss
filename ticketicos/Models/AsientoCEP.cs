using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ticketicos.Models
{
    [Table("AsientosCEP")]
    public class AsientoCEP
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id_asiento")]
        public int IdAsiento { get; set; }

        [Column("id_bloqueCEP")]
        public int IdBloque { get; set; }

        [Column("numero_asiento")]
        public string NumeroAsiento { get; set; }

        [Column("es_vip")]
        public bool EsVip { get; set; }

        [Column("esta_disponible")]
        public bool EstaDisponible { get; set; }

        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; }

        [Column("habilitada")]
        public bool Habilitada { get; set; }
    }
}
