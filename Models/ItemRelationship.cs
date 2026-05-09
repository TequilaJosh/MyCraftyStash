using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    public class ItemRelationship
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        public int ItemId { get; set; }
        
        public int RelatedItemId { get; set; }
        
        [ForeignKey(nameof(ItemId))]
        public virtual Item? Item { get; set; }
        
        [ForeignKey(nameof(RelatedItemId))]
        public virtual Item? RelatedItem { get; set; }
    }
}
