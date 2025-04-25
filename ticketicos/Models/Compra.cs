using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ticketicos.Models
{
    [Table("Compras")]
    public class Compra
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id_compra")]
        public int IdCompra { get; set; }

        [Column("id_evento")]
        public int IdEvento { get; set; }

        [Column("id_bloque")]
        public int IdBloque { get; set; }

        [Column("numero_asiento")]
        public string NumeroAsiento { get; set; }

        [Column("precio_entrada")]
        public decimal? PrecioEntrada { get; set; }

        [Column("fecha_compra")]
        public DateTime? FechaCompra { get; set; }

        [Column("id_pago")]
        public int IdPago { get; set; }

        // Relaciones (opcional pero útil)
        [ForeignKey("IdEvento")]
        public Evento Evento { get; set; }

        [ForeignKey("IdPago")]
        public PagoEntrada Pago { get; set; }

        [ForeignKey("IdBloque")]
        public Bloque Bloque { get; set; }

        [Column("tipo_entrada")]
        public string? TipoEntrada { get; set; }


        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Column("total")]
        public decimal Total { get; set; }

        [Column("estado")]
        public string Estado { get; set; }

        [Column("fecha")]
        public DateTime Fecha { get; set; }

        [Column("numero_factura")]
        public string NumeroFactura { get; set; }

        [Column("id_codigo_promocional")]
        public int? IdCodigoPromocional { get; set; }

        [Column("descuento_aplicado")]
        public decimal DescuentoAplicado { get; set; }


    }
}
