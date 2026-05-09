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

        // ── Project sharing ─────────────────────────────────────────────────
        // Set when this project was imported from a .mcsproject bundle sent
        // by another user. SharedFromName is the original creator (free-form
        // string from the manifest). The Projects view shows a "Shared" tag
        // on the card and reuses the same edit/build flow as personal
        // projects so the receiver can replicate the build step-by-step.

        public bool IsShared { get; set; } = false;
        public string? SharedFromName { get; set; }
        public DateTime? SharedAt { get; set; }

        public virtual ICollection<ProjectItem> ProjectItems { get; set; } = new List<ProjectItem>();
        public virtual ICollection<ProjectImage> Images { get; set; } = new List<ProjectImage>();
        public virtual ICollection<ProjectCreation> Creations { get; set; } = new List<ProjectCreation>();
    }
}
