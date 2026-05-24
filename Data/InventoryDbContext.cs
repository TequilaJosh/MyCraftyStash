using Microsoft.EntityFrameworkCore;
using JandH.Core.Data;
using JandH.Core.Models;
using JandH.Core.Services;
using MyCraftyStash.Services;

using JandH.Core.ViewModels;

namespace MyCraftyStash.Data
{
    /// <summary>
    /// My Crafty Stash's concrete inventory context. Inherits all DbSets and
    /// snake_case mappings from <see cref="BaseInventoryDbContext"/>, wires up
    /// SQLite via <see cref="AppPaths"/>, maps the MCS-only Project sharing
    /// columns, and switches FK cascade behaviors back from the SQL Server
    /// NoAction workarounds to plain SQLite cascade / SetNull.
    /// </summary>
    public class InventoryDbContext : BaseInventoryDbContext
    {
        public InventoryDbContext() { }

        public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite(AppPaths.InventoryConnectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Project sharing columns ─────────────────────────────────────
            // The shared Project model exposes IsShared / SharedFromName /
            // SharedAt for the .mcsproject import flow. JandH's DbContext
            // .Ignore()s these; MCS persists them.
            modelBuilder.Entity<Project>(entity =>
            {
                entity.Property(e => e.IsShared).HasColumnName("is_shared");
                entity.Property(e => e.SharedFromName).HasColumnName("shared_from_name");
                entity.Property(e => e.SharedAt).HasColumnName("shared_at");
            });

            // ── SQLite FK behavior overrides ────────────────────────────────
            // The base class uses DeleteBehavior.NoAction in a few places to
            // sidestep SQL Server's "multiple cascade paths" restriction.
            // SQLite has no such restriction so we restore the natural
            // Cascade / SetNull behavior MCS's migrations were created with.
            modelBuilder.Entity<ItemRelationship>()
                .HasOne(ir => ir.RelatedItem)
                .WithMany(i => i.RelatedTo)
                .HasForeignKey(ir => ir.RelatedItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectCardBuildStep>()
                .HasOne(s => s.Item)
                .WithMany()
                .HasForeignKey(s => s.ItemId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ProjectCardBuildStep>()
                .HasOne(s => s.StackletDie)
                .WithMany()
                .HasForeignKey(s => s.StackletDieId)
                .OnDelete(DeleteBehavior.SetNull);

            // InspirationImage → InspirationBoard FK behaves as SetNull in
            // MCS (vs the base's default). Override.
            modelBuilder.Entity<InspirationImage>()
                .HasOne<InspirationBoard>()
                .WithMany()
                .HasForeignKey(i => i.BoardId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
