using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    public class ProjectItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        public int ProjectId { get; set; }

        public int ItemId { get; set; }

        public int SortOrder { get; set; }

        /// <summary>
        /// For tracked items: how much is used per project creation.
        /// Cardstock = fraction of sheet (e.g. 0.5 = half sheet). 
        /// Envelopes / Foil-its / Foils = 1 unit per use.
        /// Null = not tracked.
        /// </summary>
        public decimal? AmountUsedPerCreation { get; set; }
        
        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }
        
        [ForeignKey(nameof(ItemId))]
        public virtual Item? Item { get; set; }
    }
}
