using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    [Table("wishlist_items")]
    public class WishlistItem
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("name")]
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Column("type")]
        [MaxLength(100)]
        public string? Type { get; set; }

        [Column("item_number")]
        [MaxLength(100)]
        public string? ItemNumber { get; set; }

        [Column("theme")]
        [MaxLength(255)]
        public string? Theme { get; set; }

        [Column("price")]
        public decimal? Price { get; set; }

        [Column("image_url")]
        public string? ImageUrl { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("priority")]
        public int Priority { get; set; } = 1; // 1=Low, 2=Medium, 3=High

        [Column("purchased_from")]
        [MaxLength(255)]
        public string? PurchasedFrom { get; set; }

        [Column("url")]
        [MaxLength(1000)]
        public string? Url { get; set; }

        [Column("wishlist_id")]
        public int? WishlistId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public string PriorityLabel => Priority switch
        {
            3 => "High",
            2 => "Medium",
            _ => "Low"
        };
    }
}
