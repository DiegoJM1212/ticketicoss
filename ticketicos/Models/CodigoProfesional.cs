using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ticketicos.Models
{
    [Table("CodigosProfecionales")]
    public class CodigoProfesional
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Codigo { get; set; }
    }
}
