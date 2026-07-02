using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    public class Item
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string Type { get; set; } = string.Empty;
        
        [MaxLength(255)]
        public string? Location { get; set; }
        
        [MaxLength(255)]
        public string? Theme { get; set; }
        
        public string? Sentiments { get; set; }
        
        public string? ImageUrl { get; set; }
        
        public decimal? Price { get; set; }
        
        public DateTime? DatePurchased { get; set; }
        
        [MaxLength(100)]
        public string? ItemNumber { get; set; }

        [MaxLength(30)]
        
        public bool IsDiscontinued { get; set; } = false;
        
        [MaxLength(255)]
        public string? Subtype { get; set; }
        
        public int? StencilLayers { get; set; }
        
        /// <summary>How many sheets/items come in one pack (for Cardstock, Envelopes, Foil-its, Foils)</summary>
        public int? PackSize { get; set; }
        
        /// <summary>Current inventory count of individual items remaining</summary>
        public int? CurrentStock { get; set; }
        
        [MaxLength(255)]
        public string? PurchasedFrom { get; set; }
        
        public string? Notes { get; set; }

        /// <summary>Optional URL to the product page, used for quick repurchasing.</summary>
        [MaxLength(1000)]
        public string? SiteUrl { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public virtual ICollection<ItemRelationship> RelatedFrom { get; set; } = new List<ItemRelationship>();
        public virtual ICollection<ItemRelationship> RelatedTo { get; set; } = new List<ItemRelationship>();
        public virtual ICollection<ProjectItem> ProjectItems { get; set; } = new List<ProjectItem>();
        public virtual ICollection<ItemImage> Images { get; set; } = new List<ItemImage>();
        public virtual ICollection<ItemPurchase> Purchases { get; set; } = new List<ItemPurchase>();
        public virtual ICollection<ItemSale> Sales { get; set; } = new List<ItemSale>();

        // ── Transient bought-vs-sold counts (not persisted) ──────────────────
        // Populated by InventoryService when loading items so cards can show a
        // "Sold" badge. TotalBought = sum of purchase quantities; TotalSold =
        // sum of sale quantities.
        [NotMapped]
        public int TotalBought { get; set; }

        [NotMapped]
        public int TotalSold { get; set; }

        /// <summary>True once every purchased unit has been sold (the user sold
        /// their last remaining one). Guards on TotalBought so items with no
        /// recorded purchases never falsely read as sold.</summary>
        [NotMapped]
        public bool IsSoldOut => TotalBought > 0 && TotalSold >= TotalBought;
    }
}
