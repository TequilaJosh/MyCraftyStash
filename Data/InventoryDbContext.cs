using Microsoft.EntityFrameworkCore;
using MyCraftyStash.Models;
using MyCraftyStash.Services;

namespace MyCraftyStash.Data
{
    public class InventoryDbContext : DbContext
    {
        public DbSet<Item> Items { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ItemRelationship> ItemRelationships { get; set; }
        public DbSet<ProjectItem> ProjectItems { get; set; }
        public DbSet<ItemImage> ItemImages { get; set; }
        public DbSet<ProjectImage> ProjectImages { get; set; }
        public DbSet<ItemPurchase> ItemPurchases { get; set; }
        public DbSet<ItemSale> ItemSales { get; set; }
        public DbSet<InspirationImage> InspirationImages { get; set; }
        public DbSet<InspirationBoard> InspirationBoards { get; set; }
        public DbSet<SentimentImage> SentimentImages { get; set; }
        public DbSet<InspirationImageItem> InspirationImageItems { get; set; }
        public DbSet<HiddenInspirationImage> HiddenInspirationImages { get; set; }
        public DbSet<ProjectCreation> ProjectCreations { get; set; }
        public DbSet<StackletDie> StackletDies { get; set; }
        public DbSet<ProjectCardBuild> ProjectCardBuilds { get; set; }
        public DbSet<ProjectCardBuildStep> ProjectCardBuildSteps { get; set; }
        public DbSet<AddressBookEntry> AddressBookEntries { get; set; }
        public DbSet<CalendarEvent> CalendarEvents { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<Wishlist> Wishlists { get; set; }

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
            // Map to lowercase table names (matching the previous SQL Server schema).
            modelBuilder.Entity<Item>().ToTable("items");
            modelBuilder.Entity<Project>().ToTable("projects");
            modelBuilder.Entity<ItemImage>().ToTable("item_images");
            modelBuilder.Entity<ProjectImage>().ToTable("project_images");
            modelBuilder.Entity<ProjectItem>().ToTable("project_items");
            modelBuilder.Entity<ItemRelationship>().ToTable("item_relationships");
            modelBuilder.Entity<ItemPurchase>().ToTable("item_purchases");
            modelBuilder.Entity<ItemSale>().ToTable("item_sales");

            modelBuilder.Entity<Item>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Type).HasColumnName("type");
                entity.Property(e => e.Location).HasColumnName("location");
                entity.Property(e => e.Theme).HasColumnName("theme");
                entity.Property(e => e.Sentiments).HasColumnName("sentiments");
                entity.Property(e => e.ImageUrl).HasColumnName("image_url");
                entity.Property(e => e.Price).HasColumnName("price");
                entity.Property(e => e.DatePurchased).HasColumnName("date_purchased");
                entity.Property(e => e.ItemNumber).HasColumnName("item_number");
                entity.Property(e => e.IsDiscontinued).HasColumnName("is_discontinued");
                entity.Property(e => e.StencilLayers).HasColumnName("stencil_layers");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.Subtype).HasColumnName("subtype");
                entity.Property(e => e.PackSize).HasColumnName("pack_size");
                entity.Property(e => e.CurrentStock).HasColumnName("current_stock");
                entity.Property(e => e.PurchasedFrom).HasColumnName("purchased_from");
                entity.Property(e => e.Notes).HasColumnName("notes");
                entity.Property(e => e.SiteUrl).HasColumnName("site_url");
            });

            modelBuilder.Entity<Project>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.ImageUrl).HasColumnName("image_url");
                entity.Property(e => e.Technique).HasColumnName("technique");
                entity.Property(e => e.Notes).HasColumnName("notes");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.IsShared).HasColumnName("is_shared");
                entity.Property(e => e.SharedFromName).HasColumnName("shared_from_name");
                entity.Property(e => e.SharedAt).HasColumnName("shared_at");
            });

            modelBuilder.Entity<ItemImage>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ItemId).HasColumnName("item_id");
                entity.Property(e => e.ImageUrl).HasColumnName("image_url");
                entity.Property(e => e.SortOrder).HasColumnName("sort_order");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            modelBuilder.Entity<ProjectImage>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ProjectId).HasColumnName("project_id");
                entity.Property(e => e.ImageUrl).HasColumnName("image_url");
                entity.Property(e => e.SortOrder).HasColumnName("sort_order");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            modelBuilder.Entity<ItemPurchase>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ItemId).HasColumnName("item_id");
                entity.Property(e => e.Quantity).HasColumnName("quantity");
                entity.Property(e => e.PricePerItem).HasColumnName("price_per_item");
                entity.Property(e => e.DatePurchased).HasColumnName("date_purchased");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            modelBuilder.Entity<ItemSale>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ItemId).HasColumnName("item_id");
                entity.Property(e => e.Quantity).HasColumnName("quantity");
                entity.Property(e => e.SalePrice).HasColumnName("sale_price");
                entity.Property(e => e.DateSold).HasColumnName("date_sold");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            modelBuilder.Entity<ProjectItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
                entity.Property(e => e.ProjectId).HasColumnName("project_id");
                entity.Property(e => e.ItemId).HasColumnName("item_id");
                entity.Property(e => e.AmountUsedPerCreation).HasColumnName("amount_used_per_creation");
                entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            });

            modelBuilder.Entity<ItemRelationship>(entity =>
            {
                entity.HasKey(e => new { e.ItemId, e.RelatedItemId });
                entity.Property(e => e.ItemId).HasColumnName("item_id");
                entity.Property(e => e.RelatedItemId).HasColumnName("related_item_id");
                entity.Ignore(e => e.Id);
            });

            // SQLite has no "multiple cascade paths" restriction, so all FKs cascade
            // freely. The old SQL Server-only NoAction workarounds are gone.
            modelBuilder.Entity<ItemRelationship>()
                .HasOne(ir => ir.Item)
                .WithMany(i => i.RelatedFrom)
                .HasForeignKey(ir => ir.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ItemRelationship>()
                .HasOne(ir => ir.RelatedItem)
                .WithMany(i => i.RelatedTo)
                .HasForeignKey(ir => ir.RelatedItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectItem>()
                .HasOne(pi => pi.Project)
                .WithMany(p => p.ProjectItems)
                .HasForeignKey(pi => pi.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectItem>()
                .HasOne(pi => pi.Item)
                .WithMany(i => i.ProjectItems)
                .HasForeignKey(pi => pi.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ItemImage>()
                .HasOne(ii => ii.Item)
                .WithMany(i => i.Images)
                .HasForeignKey(ii => ii.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectImage>()
                .HasOne(pi => pi.Project)
                .WithMany(p => p.Images)
                .HasForeignKey(pi => pi.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ItemPurchase>()
                .HasOne(ip => ip.Item)
                .WithMany(i => i.Purchases)
                .HasForeignKey(ip => ip.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ItemSale>()
                .HasOne(s => s.Item)
                .WithMany(i => i.Sales)
                .HasForeignKey(s => s.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // InspirationBoard
            modelBuilder.Entity<InspirationBoard>().ToTable("inspiration_boards");
            modelBuilder.Entity<InspirationBoard>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.ParentBoardId).HasColumnName("parent_board_id");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.DisplayOrder).HasColumnName("display_order");
                entity.Property(e => e.DefaultTypes).HasColumnName("default_types");
                entity.Property(e => e.DefaultThemes).HasColumnName("default_themes");
                entity.Property(e => e.DefaultColors).HasColumnName("default_colors");
                entity.Property(e => e.DefaultSentiment).HasColumnName("default_sentiment");
                entity.Property(e => e.DefaultTeColors).HasColumnName("default_te_colors");
            });

            modelBuilder.Entity<InspirationBoard>()
                .HasOne<InspirationBoard>()
                .WithMany()
                .HasForeignKey(b => b.ParentBoardId)
                .OnDelete(DeleteBehavior.Restrict);

            // InspirationImage
            modelBuilder.Entity<InspirationImage>().ToTable("inspiration_images");
            modelBuilder.Entity<InspirationImage>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ImageUrl).HasColumnName("image_url");
                entity.Property(e => e.Title).HasColumnName("title");
                entity.Property(e => e.Notes).HasColumnName("notes");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.BoardId).HasColumnName("board_id");
                entity.Property(e => e.Color).HasColumnName("color");
                entity.Property(e => e.Types).HasColumnName("types");
                entity.Property(e => e.Theme).HasColumnName("theme");
                entity.Property(e => e.Sentiment).HasColumnName("sentiment");
                entity.Property(e => e.TeColor).HasColumnName("te_color");
            });

            modelBuilder.Entity<InspirationImage>()
                .HasOne<InspirationBoard>()
                .WithMany()
                .HasForeignKey(i => i.BoardId)
                .OnDelete(DeleteBehavior.SetNull);

            // SentimentImage
            modelBuilder.Entity<SentimentImage>().ToTable("sentiment_images");
            modelBuilder.Entity<SentimentImage>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ItemId).HasColumnName("item_id");
                entity.Property(e => e.ImageData).HasColumnName("image_data");
                entity.Property(e => e.ExtractedText).HasColumnName("extracted_text");
                entity.Property(e => e.SearchText).HasColumnName("search_text");
                entity.Property(e => e.SortOrder).HasColumnName("sort_order");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            modelBuilder.Entity<SentimentImage>()
                .HasOne(si => si.Item)
                .WithMany()
                .HasForeignKey(si => si.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InspirationImageItem>().ToTable("inspiration_image_items");
            modelBuilder.Entity<InspirationImageItem>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.InspirationImageId).HasColumnName("inspiration_image_id");
                entity.Property(e => e.ItemId).HasColumnName("item_id");
            });

            modelBuilder.Entity<InspirationImageItem>()
                .HasOne(ii => ii.InspirationImage)
                .WithMany()
                .HasForeignKey(ii => ii.InspirationImageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InspirationImageItem>()
                .HasOne(ii => ii.Item)
                .WithMany()
                .HasForeignKey(ii => ii.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HiddenInspirationImage>().ToTable("hidden_inspiration_images");
            modelBuilder.Entity<HiddenInspirationImage>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ImageKey).HasColumnName("image_key");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            // ProjectCreation
            modelBuilder.Entity<ProjectCreation>().ToTable("project_creations");
            modelBuilder.Entity<ProjectCreation>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ProjectId).HasColumnName("project_id");
                entity.Property(e => e.CreatedOn).HasColumnName("created_on");
                entity.Property(e => e.Notes).HasColumnName("notes");
                entity.Property(e => e.MaterialsUsed).HasColumnName("materials_used");
            });

            modelBuilder.Entity<ProjectCreation>()
                .HasOne(pc => pc.Project)
                .WithMany(p => p.Creations)
                .HasForeignKey(pc => pc.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // AddressBookEntry
            modelBuilder.Entity<AddressBookEntry>().ToTable("address_book");
            modelBuilder.Entity<AddressBookEntry>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.FirstName).HasColumnName("first_name");
                entity.Property(e => e.LastName).HasColumnName("last_name");
                entity.Property(e => e.AddressLine1).HasColumnName("address_line1");
                entity.Property(e => e.AddressLine2).HasColumnName("address_line2");
                entity.Property(e => e.City).HasColumnName("city");
                entity.Property(e => e.State).HasColumnName("state");
                entity.Property(e => e.ZipCode).HasColumnName("zip_code");
                entity.Property(e => e.Country).HasColumnName("country");
                entity.Property(e => e.Phone).HasColumnName("phone");
                entity.Property(e => e.Email).HasColumnName("email");
                entity.Property(e => e.Notes).HasColumnName("notes");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            });

            // CalendarEvent
            modelBuilder.Entity<CalendarEvent>().ToTable("calendar_events");
            modelBuilder.Entity<CalendarEvent>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Title).HasColumnName("title");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.EventDate).HasColumnName("event_date");
                entity.Property(e => e.EventTime).HasColumnName("event_time");
                entity.Property(e => e.IsAllDay).HasColumnName("is_all_day");
                entity.Property(e => e.ReminderMinutesBefore).HasColumnName("reminder_minutes_before");
                entity.Property(e => e.Color).HasColumnName("color");
                entity.Property(e => e.ReminderDismissed).HasColumnName("reminder_dismissed");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            });

            // Wishlist
            modelBuilder.Entity<Wishlist>().ToTable("wishlists");
            modelBuilder.Entity<Wishlist>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Color).HasColumnName("color");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            // WishlistItem
            modelBuilder.Entity<WishlistItem>().ToTable("wishlist_items");
            modelBuilder.Entity<WishlistItem>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Type).HasColumnName("type");
                entity.Property(e => e.ItemNumber).HasColumnName("item_number");
                entity.Property(e => e.Theme).HasColumnName("theme");
                entity.Property(e => e.Price).HasColumnName("price");
                entity.Property(e => e.ImageUrl).HasColumnName("image_url");
                entity.Property(e => e.Notes).HasColumnName("notes");
                entity.Property(e => e.Priority).HasColumnName("priority");
                entity.Property(e => e.PurchasedFrom).HasColumnName("purchased_from");
                entity.Property(e => e.Url).HasColumnName("url");
                entity.Property(e => e.WishlistId).HasColumnName("wishlist_id");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            modelBuilder.Entity<WishlistItem>()
                .HasOne<Wishlist>()
                .WithMany()
                .HasForeignKey(w => w.WishlistId)
                .OnDelete(DeleteBehavior.SetNull);

            // StackletDie
            modelBuilder.Entity<StackletDie>().ToTable("stacklet_dies");
            modelBuilder.Entity<StackletDie>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ItemId).HasColumnName("item_id");
                entity.Property(e => e.DieNumber).HasColumnName("die_number");
                entity.Property(e => e.Width).HasColumnName("width");
                entity.Property(e => e.Height).HasColumnName("height");
                entity.Property(e => e.Label).HasColumnName("label");
            });
            modelBuilder.Entity<StackletDie>()
                .HasOne(sd => sd.Item)
                .WithMany()
                .HasForeignKey(sd => sd.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // ProjectCardBuild
            modelBuilder.Entity<ProjectCardBuild>().ToTable("project_card_builds");
            modelBuilder.Entity<ProjectCardBuild>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ProjectId).HasColumnName("project_id");
                entity.Property(e => e.CardBaseType).HasColumnName("card_base_type");
                entity.Property(e => e.StateSnapshot).HasColumnName("state_snapshot");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });
            modelBuilder.Entity<ProjectCardBuild>()
                .HasOne(pcb => pcb.Project)
                .WithMany()
                .HasForeignKey(pcb => pcb.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // ProjectCardBuildStep
            modelBuilder.Entity<ProjectCardBuildStep>().ToTable("project_card_build_steps");
            modelBuilder.Entity<ProjectCardBuildStep>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.BuildId).HasColumnName("build_id");
                entity.Property(e => e.StepOrder).HasColumnName("step_order");
                entity.Property(e => e.Section).HasColumnName("section");
                entity.Property(e => e.StepType).HasColumnName("step_type");
                entity.Property(e => e.MatLayer).HasColumnName("mat_layer");
                entity.Property(e => e.ItemId).HasColumnName("item_id");
                entity.Property(e => e.StackletDieId).HasColumnName("stacklet_die_id");
                entity.Property(e => e.CuttingMethod).HasColumnName("cutting_method");
                entity.Property(e => e.Label).HasColumnName("label");
            });
            modelBuilder.Entity<ProjectCardBuildStep>()
                .HasOne(s => s.Build)
                .WithMany(b => b.Steps)
                .HasForeignKey(s => s.BuildId)
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
        }
    }
}
