using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ticketicos.Models
{
    [Table("Usuarios")]
    public class Usuario
    {
        [Key]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; }

        [Column("apellidos")]
        public string Apellidos { get; set; }

        [Column("correo")]
        public string Correo { get; set; }

        [Column("contrasena")]
        public string Contrasena { get; set; }

        [Column("identificacion")]
        public string Identificacion { get; set; }

        [Column("telefono")]
        public string Telefono { get; set; }

        [Column("tipo_usuario")]
        public string TipoUsuario { get; set; }

        [Column("estado")]
        public string Estado { get; set; }

        [Column("fecha_registro")]
        public DateTime FechaRegistro { get; set; }

        [Column("ultima_fecha_acceso")]
        public DateTime? UltimaFechaAcceso { get; set; } // Puede ser NULL en la base

        [Column("doble_factor_habilitado")]
        public bool DobleFactorHabilitado { get; set; }
    }

    public class Usuarios
    {
        public int IdUsuario { get; set; } // Identificador único del usuario
        public string Correo { get; set; } = string.Empty; // Correo electrónico del usuario
        public string Contrasena { get; set; } = string.Empty; // Contraseña del usuario
        public string Nombre { get; set; } = string.Empty; // Nombre del usuario
        public string Apellido { get; set; } = string.Empty; // Apellido del usuario
        public DateTime FechaRegistro { get; set; } // Fecha en que se registró el usuario
    }
}

