using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Models;
using MyCraftyStash.Services;

namespace MyCraftyStash.Data
{
    /// <summary>
    /// Per-install settings + shared config lists. Lives in settings.db, kept
    /// strictly separate from the inventory DB so wiping one never touches the
    /// other.
    /// </summary>
    public class SettingsDbContext : DbContext
    {
        public DbSet<KvSetting> KvSettings { get; set; }
        public DbSet<TypeSortOrderEntry> TypeSortOrders { get; set; }
        public DbSet<CustomColor> CustomColors { get; set; }
        public DbSet<ConfigList> ConfigLists { get; set; }
        public DbSet<ColorMatch> ColorMatches { get; set; }

        public SettingsDbContext() { }

        public SettingsDbContext(DbContextOptions<SettingsDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite(AppPaths.SettingsConnectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<KvSetting>(e =>
            {
                e.ToTable("kv_settings");
                e.HasKey(x => x.Key);
                e.Property(x => x.Key).HasColumnName("key");
                e.Property(x => x.Value).HasColumnName("value");
            });

            modelBuilder.Entity<TypeSortOrderEntry>(e =>
            {
                e.ToTable("type_sort_orders");
                e.HasKey(x => x.Type);
                e.Property(x => x.Type).HasColumnName("type");
                e.Property(x => x.Sort1).HasColumnName("sort1");
                e.Property(x => x.Sort2).HasColumnName("sort2");
                e.Property(x => x.Sort3).HasColumnName("sort3");
            });

            modelBuilder.Entity<CustomColor>(e =>
            {
                e.ToTable("custom_colors");
                e.HasKey(x => x.BrushKey);
                e.Property(x => x.BrushKey).HasColumnName("brush_key");
                e.Property(x => x.Hex).HasColumnName("hex");
            });

            modelBuilder.Entity<ConfigList>(e =>
            {
                e.ToTable("config_lists");
                e.HasKey(x => x.Name);
                e.Property(x => x.Name).HasColumnName("name");
                e.Property(x => x.Content).HasColumnName("content");
            });

            modelBuilder.Entity<ColorMatch>(e =>
            {
                e.ToTable("color_matches");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.System).HasColumnName("system");
                e.Property(x => x.ExternalCode).HasColumnName("external_code");
                e.Property(x => x.TeColorName).HasColumnName("te_color_name");
                e.Property(x => x.ExternalHex).HasColumnName("external_hex");
                e.Property(x => x.TeColorHex).HasColumnName("te_color_hex");
                e.Property(x => x.Notes).HasColumnName("notes");
                // Look-ups are always (system, code) so index them together.
                e.HasIndex(x => new { x.System, x.ExternalCode }).IsUnique();
                e.HasIndex(x => new { x.System, x.TeColorName });
            });
        }
    }
}
