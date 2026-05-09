using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    public class ProjectCardBuild
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        [MaxLength(100)]
        public string CardBaseType { get; set; } = string.Empty;

        /// <summary>
        /// JSON snapshot of the wizard's full state at save time. Lets the wizard
        /// rehydrate every collection / picker selection when re-editing the build,
        /// rather than parsing the human-readable Step labels.
        /// </summary>
        public string? StateSnapshot { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }

        public virtual List<ProjectCardBuildStep> Steps { get; set; } = new();
    }
}
