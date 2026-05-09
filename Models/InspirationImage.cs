using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    public class InspirationImage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        public string ImageUrl { get; set; } = string.Empty;
        
        [MaxLength(255)]
        public string? Title { get; set; }
        
        public string? Notes { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? BoardId { get; set; }

        /// <summary>Comma-separated list of selected colors</summary>
        [MaxLength(500)]
        public string? Color { get; set; }

        /// <summary>Comma-separated list of selected types</summary>
        [MaxLength(500)]
        public string? Types { get; set; }

        /// <summary>Comma-separated list of selected themes</summary>
        [MaxLength(500)]
        public string? Theme { get; set; }

        [MaxLength(500)]
        public string? Sentiment { get; set; }

        /// <summary>Comma-separated list of selected TE ink/color pad colors</summary>
        [MaxLength(1000)]
        public string? TeColor { get; set; }
    }
}
