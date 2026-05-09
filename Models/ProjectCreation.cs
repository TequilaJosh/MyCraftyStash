using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    [Table("project_creations")]
    public class ProjectCreation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.Now;

        [MaxLength(500)]
        public string? Notes { get; set; }

        /// <summary>JSON-serialized list of {ItemId, AmountUsed} for stock deduction audit</summary>
        public string? MaterialsUsed { get; set; }

        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }
    }
}
