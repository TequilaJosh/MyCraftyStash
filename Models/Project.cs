using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    public class Project
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        
        public string? Description { get; set; }
        
        public string? ImageUrl { get; set; }
        
        public string? Technique { get; set; }
        
        public string? Notes { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public virtual ICollection<ProjectItem> ProjectItems { get; set; } = new List<ProjectItem>();
        public virtual ICollection<ProjectImage> Images { get; set; } = new List<ProjectImage>();
        public virtual ICollection<ProjectCreation> Creations { get; set; } = new List<ProjectCreation>();
    }
}
