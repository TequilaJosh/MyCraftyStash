using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyCraftyStash.Data
{
    /// <summary>
    /// Used only by `dotnet ef` at design time. WPF apps publish as a single
    /// .exe with no companion .dll, which trips up EF's default assembly probe;
    /// supplying an explicit factory sidesteps that. Path doesn't matter — EF
    /// only inspects the model when scaffolding migrations.
    /// </summary>
    public class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
    {
        public InventoryDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<InventoryDbContext>()
                .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), "mycraftystash_design.db")}")
                .Options;
            return new InventoryDbContext(options);
        }
    }
}
