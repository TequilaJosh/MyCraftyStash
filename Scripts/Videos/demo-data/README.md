# Demo Data for Video Recording

This folder seeds the My Crafty Stash database with a realistic-looking but fully synthetic dataset, designed to make every feature in the video scripts ([../01–07](..)) demo well on camera.

## What gets seeded

| Area | Rows | Purpose / which video |
|---|---:|---|
| `items` | 40 | Inventory grid + search + filter demos (videos 01, 02) |
| `item_relationships` | 8 | "Related Items" sidebar (video 02) |
| `item_purchases` | 60+ | Purchase history table + Purchase Report totals (video 02) |
| `projects` | 8 | Project grid + detail (video 03), incl. one **Shared** project |
| `project_images` | 16 | Project galleries (video 03) |
| `project_items` | 35 | "Items Used" sidebar (video 03) |
| `project_creations` | 14 | Creation history rows incl. **I Made One!** (videos 03 & 04) |
| `project_card_builds` + `_steps` | 3 builds, ~28 steps | **How It Was Made** card + wizard re-open (video 04) |
| `wishlists` + `wishlist_items` | 4 lists, 22 items | Color-coded tabs, running totals, TE-import demo (video 06) |
| `address_book` | 8 | Address Book view |
| `calendar_events` | 6 | Calendar + reminder dialog |
| `sentiment_images` | 6 | "Sentiments from this Set" card on Item Detail (video 02) |
| `inspiration_boards` + `inspiration_images` | 3 boards, 12 images | Inspiration view |

The numbers are tuned so the **Home dashboard** ([video 01](../01_home_dashboard.md)) shows a believable mix on screen: positive month-to-date deltas on Total Items and Projects, ~5 items in the **LOW / OUT** card, and 2-column "Recent projects" populated.

## File layout

```
demo-data/
├── README.md              ← you are here
├── SeedDemoData.sql       ← the actual seed
└── WipeDemoData.sql       ← removes only demo rows (id >= 9000)
```

All demo rows use **explicit IDs starting at 9000** in every table, so the wipe is a safe `DELETE … WHERE id >= 9000` per table. Real user data with smaller IDs is untouched.

## How to apply

### Prerequisites
- The MCS app has been launched at least once on this machine, so `inventory.db` exists.
- The `sqlite3` CLI is installed (`winget install SQLite.SQLite` or download from sqlite.org).
- **MCS is closed** while you run the script — SQLite locks the file.

### Find your DB file

By default, `inventory.db` lives next to the executable:

```powershell
%LOCALAPPDATA%\Programs\My Crafty Stash\inventory.db
```

If the install folder isn't writable (dev builds, USB stick) it falls back to:

```powershell
%LOCALAPPDATA%\My Crafty Stash\inventory.db
```

Confirm by running MCS once and checking `Settings → About → Data folder` (if exposed), or just check whichever exists.

### Apply the seed

```powershell
$db = "$env:LOCALAPPDATA\Programs\My Crafty Stash\inventory.db"
# Safety: back up your real DB first
Copy-Item $db "$db.before-seed.bak"
# Seed
sqlite3 $db ".read $PSScriptRoot\SeedDemoData.sql"
```

### Remove the seed afterwards

```powershell
sqlite3 $db ".read $PSScriptRoot\WipeDemoData.sql"
```

Or restore the backup:

```powershell
Copy-Item "$db.before-seed.bak" $db -Force
```

## What you'll see in each video after seeding

### [01 — Home Dashboard](../01_home_dashboard.md)
- **TOTAL ITEMS**: 40 (with a green "▲ 6 this month" delta from items dated within the last 30 days)
- **LOW / OUT**: 5 items flagged (current_stock = 0 or 1)
- **PROJECTS**: 8 (with a "▲ 2 this month" delta)
- **Running low** panel: 5 items with progress bars
- **Recent projects**: 4 most recent project thumbnails

### [02 — Inventory](../02_inventory.md)
- Searching **"thank you"** finds hits across multiple sentiment-tagged stamp sets
- Multiple types & subtypes populated so the **Type** dropdown and dynamic subtype checkboxes are meaningful
- A `Sentiment Set — Big Thanks` item has 3 sentiment-image snips so the "Sentiments from this Set" card shows on its detail view
- Stock counts vary (0, 1, 2, 5, 12, etc.) so the stock badge demo is interesting
- The same item has 4 purchase-history rows over the last year

### [03 — Projects](../03_projects.md)
- 8 projects spanning 3 months of history
- One project (`Imported — Friend's Sympathy Card`) has `is_shared = 1` so the **"Shared"** badge appears
- "I Made One!" creation history on most projects (avg 2 creations each)
- Several projects share items — so filtering by **Specific Item** narrows projects in a satisfying way

### [04 — Card Build Wizard](../04_card_build_wizard.md)
- 3 projects have a full `project_card_build` row + steps so the **How It Was Made** card is populated and re-opening the wizard shows pre-filled "Done!" pills
- Other projects have **no build yet** so you can demo the **Build Card** button starting fresh

### [05 — Color Match](../05_color_match.md)
- Inventory includes ~10 cardstock items with named colors (Crimson, Sage, Lemon, Sky, Charcoal, etc.) so the inventory-match swatches show up after picking colors from a reference image
- *(Color Match uses uploaded images at runtime — nothing to seed for the image side. The seed only ensures matching cardstock exists.)*

### [06 — Wish List](../06_wishlist.md)
- 4 lists: **Spring Release 2026** (rose), **Birthday Cards** (blue), **Holiday 2026** (red), **Someday / Maybe** (gray)
- Each list has 4–8 items with prices that add up to a clean running total
- **Holiday 2026** is intentionally empty for the empty-state shot
- *(For the TE-import demo, you'll still need a real exported TE wishlist file — the seed can't fake the import flow itself.)*

### [07 — Ad montage](../07_ad_feature_montage.md)
Every cutaway in the ad has on-screen data because of the above.

## Re-running the seed

The seed is **not idempotent** — re-running on a DB that already has demo data will fail on the explicit `id` PK conflict. Always run `WipeDemoData.sql` first, or restore the backup.

## Troubleshooting

- **"database is locked"** — close MCS, then re-run.
- **`UNIQUE constraint failed: items.id`** — you already seeded. Run `WipeDemoData.sql` first.
- **Images aren't loading** — the seed uses `https://picsum.photos/seed/...` URLs which require internet. For an offline shoot, batch-replace those URLs with local file paths.
- **Home dashboard's "this month" numbers don't move** — the seed dates items relative to `date('now')`. If you seed and then advance your system clock more than ~30 days, the deltas will go to zero. Re-seed or edit `created_at` values manually.
</content>
</invoke>