using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    public class StackletDie
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int ItemId { get; set; }

        public int DieNumber { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? Width { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? Height { get; set; }

        [MaxLength(100)]
        public string? Label { get; set; }

        [ForeignKey(nameof(ItemId))]
        public virtual Item? Item { get; set; }
    }
}
