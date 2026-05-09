using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    [Table("item_purchases")]
    public class ItemPurchase
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        [Column("item_id")]
        public int ItemId { get; set; }
        
        [Column("quantity")]
        public int Quantity { get; set; } = 1;
        
        [Column("price_per_item", TypeName = "decimal(10,2)")]
        public decimal PricePerItem { get; set; } = 0;
        
        [Column("date_purchased")]
        public DateTime? DatePurchased { get; set; }
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public virtual Item? Item { get; set; }
        
        [NotMapped]
        public decimal TotalPrice => Quantity * PricePerItem;
    }
}
