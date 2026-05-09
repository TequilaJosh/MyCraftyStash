using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    public class InspirationBoard
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int? ParentBoardId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int DisplayOrder { get; set; }

        /// <summary>Comma-separated default types applied to images added to this board</summary>
        [MaxLength(500)]
        public string? DefaultTypes { get; set; }

        /// <summary>Comma-separated default themes applied to images added to this board</summary>
        [MaxLength(500)]
        public string? DefaultThemes { get; set; }

        /// <summary>Comma-separated default colors applied to images added to this board</summary>
        [MaxLength(500)]
        public string? DefaultColors { get; set; }

        /// <summary>Default sentiment applied to images moved to this board</summary>
        [MaxLength(500)]
        public string? DefaultSentiment { get; set; }

        /// <summary>Comma-separated default TE colors applied to images added to this board</summary>
        [MaxLength(1000)]
        public string? DefaultTeColors { get; set; }

        /// <summary>Pipe-delimited type+subtype entries, e.g. "Stamp:A2,Birthday|Die:Rectangle"</summary>
        [MaxLength(2000)]
        public string? DefaultSubtypes { get; set; }

        /// <summary>Comma-separated item IDs for types that require specific items (e.g. Ink, Cardstock)</summary>
        [MaxLength(2000)]
        public string? DefaultItemIds { get; set; }

        public bool HasDefaults =>
            !string.IsNullOrEmpty(DefaultTypes) ||
            !string.IsNullOrEmpty(DefaultThemes) ||
            !string.IsNullOrEmpty(DefaultColors) ||
            !string.IsNullOrEmpty(DefaultSentiment) ||
            !string.IsNullOrEmpty(DefaultTeColors) ||
            !string.IsNullOrEmpty(DefaultSubtypes) ||
            !string.IsNullOrEmpty(DefaultItemIds);
    }
}
