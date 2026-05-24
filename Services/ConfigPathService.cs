using System.IO;
using JandH.Core.Services;

using JandH.Core.Models;
using JandH.Core.ViewModels;

namespace MyCraftyStash.Services
{
    /// <summary>
    /// My Crafty Stash's <see cref="IConfigPaths"/> implementation. Paths live
    /// under the writable <see cref="AppPaths.DataRoot"/> so the install folder
    /// is portable.
    ///
    /// NOTE: MCS's canonical config store is <see cref="ConfigStore"/> (rows
    /// in settings.db), not these files. The paths here exist so the shared
    /// JandH.Core services that read/write text-file config (BulkRenameService,
    /// CardLabelMappingService, the file-tab in JandH's SettingsDialog) have
    /// somewhere to point — but on MCS first launch the files simply don't
    /// exist yet, and reader services already handle that gracefully.
    /// Eventually an ISettingsStore abstraction will unify the two paths.
    /// </summary>
    public class ConfigPathService : IConfigPaths
    {
        public string ConfigDir
        {
            get
            {
                var dir = Path.Combine(AppPaths.DataRoot, "Config");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public string TypesFile => Path.Combine(ConfigDir, "types.txt");
        public string ThemesFile => Path.Combine(ConfigDir, "themes.txt");
        public string LocationsFile => Path.Combine(ConfigDir, "locations.txt");
        public string ColorOrderFile => Path.Combine(ConfigDir, "ColorOrder.txt");
        public string SubtypesFile => Path.Combine(ConfigDir, "subtypes.json");
        public string TrackedTypesFile => Path.Combine(ConfigDir, "tracked_types.json");
        public string ProjectTrackedItemsFile => Path.Combine(ConfigDir, "project_tracked_items.json");
        public string PurchasedFromFile => Path.Combine(ConfigDir, "purchased_from.txt");
        public string InspirationColorsFile => Path.Combine(ConfigDir, "inspiration_colors.txt");
        public string CardLabelsFile => Path.Combine(ConfigDir, "card_labels.json");
    }
}
