using System;
using System.IO;
using System.Text.Json;

namespace MyCraftyStash.Models
{
    public class UpdateSettings
    {
        public string? UpdatePath { get; set; }
        public bool CheckForUpdatesOnStartup { get; set; } = true;
        public DateTime? LastUpdateCheck { get; set; }
        // The most recent assembly version we've shown release notes for.
        // Drives the "what's new" popup so it only fires once per upgrade.
        // Stored as a string ("1.0.5.0") so JSON round-trips cleanly.
        public string? LastNotesShownVersion { get; set; }
        
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyCraftyStash",
            "update_settings.json"
        );
        
        public static UpdateSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<UpdateSettings>(json) ?? new UpdateSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSettings load error: {ex.Message}");
            }
            return new UpdateSettings();
        }
        
        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSettings save error: {ex.Message}");
            }
        }
    }
}
