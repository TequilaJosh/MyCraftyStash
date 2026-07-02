using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    /// <summary>
    /// A record of selling one or more units of an inventory <see cref="Item"/>
    /// for a given price. The revenue-side mirror of <see cref="ItemPurchase"/>:
    /// purchases track what you spent (cost), sales track what you took in.
    /// Recording a sale decrements the item's stock for tracked types.
    /// </summary>
    [Table("item_sales")]
    public class ItemSale
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("item_id")]
        public int ItemId { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; } = 1;

        [Column("sale_price", TypeName = "decimal(10,2)")]
        public decimal SalePrice { get; set; } = 0;

        [Column("date_sold")]
        public DateTime? DateSold { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Item? Item { get; set; }

        [NotMapped]
        public decimal TotalPrice => Quantity * SalePrice;
    }
}
