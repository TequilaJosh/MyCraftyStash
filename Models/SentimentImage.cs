using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    public class SentimentImage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        public int ItemId { get; set; }
        
        public string ImageData { get; set; } = string.Empty;
        
        public string ExtractedText { get; set; } = string.Empty;
        
        public string SearchText { get; set; } = string.Empty;
        
        public int SortOrder { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [ForeignKey("ItemId")]
        public virtual Item? Item { get; set; }
    }
}
