using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using JandH.Core.Models;
using JandH.Core.Services;
using JandH.Core.ViewModels;

namespace MyCraftyStash.Data
{
    public class SettingsDbContextFactory : IDesignTimeDbContextFactory<SettingsDbContext>
    {
        public SettingsDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<SettingsDbContext>()
                .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), "mycraftystash_settings_design.db")}")
                .Options;
            return new SettingsDbContext(options);
        }
    }
}
