using System;
using System.Collections.Generic;

namespace MyCraftyStash.Models
{
    /// <summary>
    /// Configuration + state for the desktop -> cloud sync feature.
    ///
    /// Persisted via UserSettingsService.SetSettingValue using these keys:
    ///   CloudSync.Endpoint        - the SWA base URL, e.g. https://....azurestaticapps.net
    ///   CloudSync.ApiKeyDpapi     - the user's API key, encrypted via DPAPI (base64)
    ///   CloudSync.LastSyncUtc     - ISO-8601 UTC of the last successful sync run
    ///   CloudSync.LastSyncItems   - count of items pushed in the last run (informational)
    ///   CloudSync.LastSyncImages  - count of images pushed in the last run (informational)
    ///   CloudSync.ItemHashes      - JSON {"itemId": "hash"} for per-item delta detection
    ///
    /// The hash dict is the resumability story: each item's metadata + image bytes
    /// hash to a stable string; if the hash hasn't changed since last sync we skip
    /// re-uploading. That keeps an idle "Sync now" run cheap (one query + one diff)
    /// even with a 1000-item stash.
    /// </summary>
    public sealed class CloudSyncSettings
    {
        public string? EndpointUrl { get; set; }

        /// <summary>True when a DPAPI-encrypted key is stored. Plaintext is never
        /// surfaced; callers only need to know whether one is set.</summary>
        public bool HasApiKey { get; set; }

        public DateTime? LastSyncUtc { get; set; }

        public int LastSyncItemCount { get; set; }

        public int LastSyncImageCount { get; set; }

        /// <summary>Item-id to last-uploaded-content-hash. Used to skip rows
        /// that haven't changed. Empty dict on first run = upload everything.</summary>
        public Dictionary<int, string> ItemHashes { get; set; } = new();
    }
}
