using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ticketicos.Models
{
    [Table("PagosEntradas")]
    public class PagoEntrada
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id_pago")] // <-- Cambiado para que coincida con el campo en la base si lo has renombrado
        public int IdPago { get; set; }

        [Column("metodo_pago")]
        public string MetodoPago { get; set; }

        [Column("numero_tarjeta")]
        public string NumeroTarjeta { get; set; }

        [Column("vencimiento")]
        public string Vencimiento { get; set; }

        [Column("cvv")]
        public string CVV { get; set; }

        [Column("estado")]
        public string Estado { get; set; }

        [Column("propietario_tarjeta")]
        public string Propietario_tarjeta { get; set; }


        [Column("id_codigo_promocional")]
        public int? IdCodigoPromocional { get; set; }

        [Column("descuento_aplicado")]
        public decimal DescuentoAplicado { get; set; }

        // NUEVOS CAMPOS AGREGADOS
        [Column("fecha_pago")]
        public DateTime FechaPago { get; set; }

        [Column("subtotal")]
        public decimal Subtotal { get; set; }

        [Column("impuesto")]
        public decimal Impuesto { get; set; }

        [Column("servicio")]
        public decimal Servicio { get; set; }

        [Column("total_pagado")]
        public decimal TotalPagado { get; set; }
    }
}
