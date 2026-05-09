using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    public class ProjectCardBuildStep
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int BuildId { get; set; }

        public int StepOrder { get; set; }

        /// <summary>"exterior" or "inside"</summary>
        [MaxLength(50)]
        public string Section { get; set; } = string.Empty;

        /// <summary>card_base / cardstock / background_mat / embossing_folder / focal_mat / focal_cardstock / backer / backer_cardstock / sentiment / embellishment</summary>
        [MaxLength(100)]
        public string StepType { get; set; } = string.Empty;

        public int? MatLayer { get; set; }

        public int? ItemId { get; set; }

        public int? StackletDieId { get; set; }

        [MaxLength(100)]
        public string? CuttingMethod { get; set; }

        [MaxLength(500)]
        public string Label { get; set; } = string.Empty;

        [ForeignKey(nameof(BuildId))]
        public virtual ProjectCardBuild? Build { get; set; }

        [ForeignKey(nameof(ItemId))]
        public virtual Item? Item { get; set; }

        [ForeignKey(nameof(StackletDieId))]
        public virtual StackletDie? StackletDie { get; set; }
    }
}
