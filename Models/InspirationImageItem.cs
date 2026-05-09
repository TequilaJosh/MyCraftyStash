using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    public class InspirationImageItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        public int InspirationImageId { get; set; }
        
        public int ItemId { get; set; }
        
        [ForeignKey("InspirationImageId")]
        public virtual InspirationImage InspirationImage { get; set; } = null!;
        
        [ForeignKey("ItemId")]
        public virtual Item Item { get; set; } = null!;
    }
}
