using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyCraftyStash.Models
{
    public class AddressBookEntry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? LastName { get; set; }

        [MaxLength(255)]
        public string? AddressLine1 { get; set; }

        [MaxLength(255)]
        public string? AddressLine2 { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? State { get; set; }

        [MaxLength(20)]
        public string? ZipCode { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(255)]
        public string? Email { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [NotMapped]
        public string FullName => string.IsNullOrWhiteSpace(LastName)
            ? FirstName
            : $"{FirstName} {LastName}";

        /// <summary>
        /// Not persisted - set by the ViewModel when this entry is selected.
        /// Used by the list item template to highlight the selected row
        /// without needing a DataTrigger Value="{Binding}" (which is illegal in WPF).
        /// </summary>
        [NotMapped]
        public bool IsSelected { get; set; }

        [NotMapped]
        public string DisplayAddress
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(AddressLine1)) parts.Add(AddressLine1);
                if (!string.IsNullOrWhiteSpace(AddressLine2)) parts.Add(AddressLine2);
                var cityStateZip = string.Join(", ", new[] { City, State }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(ZipCode)) cityStateZip += " " + ZipCode;
                if (!string.IsNullOrWhiteSpace(cityStateZip)) parts.Add(cityStateZip);
                if (!string.IsNullOrWhiteSpace(Country)) parts.Add(Country);
                return string.Join("\n", parts);
            }
        }
    }
}
