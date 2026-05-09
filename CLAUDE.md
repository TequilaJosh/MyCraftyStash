# MyCraftyStash — Claude Code Guide

## Project Overview
**My Crafty Stash** — a WPF desktop app (.NET 8, Windows-only) for managing craft supply inventory (stamps, dies, stencils, etc.).

- **Solution**: `MyCraftyStash.sln`
- **Project file**: `MyCraftyStash/MyCraftyStash.csproj`
- **Namespace / Assembly**: `MyCraftyStash`
- **Version**: 1.0.2
- **Target**: `net8.0-windows10.0.19041.0`

## Architecture — MVVM
```
MyCraftyStash/
├── Models/          # EF Core data models (Item, Project, WishlistItem, etc.)
├── ViewModels/      # CommunityToolkit.Mvvm ObservableObject ViewModels
├── Views/           # XAML views + code-behind
├── Services/        # Business logic and data access
├── Data/            # InventoryDbContext (EF Core)
├── Converters/      # XAML value converters
├── Controls/        # Custom WPF controls (VirtualizingWrapPanel)
├── App.xaml(.cs)    # App startup, global error handling
└── MainWindow.xaml(.cs)  # Shell window (DataContext = MainViewModel)
```

## Key Technologies / NuGet Packages
| Package | Purpose |
|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` 8.0.0 | ORM / SQL Server |
| `CommunityToolkit.Mvvm` 8.2.2 | MVVM (ObservableProperty, RelayCommand) |
| `Magick.NET-Q8-AnyCPU` 14.13.0 | Image processing |
| `Microsoft.Web.WebView2` | Envelope Expert embedded browser |
| `Microsoft.Extensions.Configuration.Json` | appsettings.json loading |

## Database
- **Provider**: SQL Server only (no SQLite/PostgreSQL despite README mentioning them)
- **Connection string**: in `appsettings.json` → `ConnectionStrings:DefaultConnection`
- **Server**: `WIN-U5IQ2HISNH3`, Database: `JandHGreetings`
- **Table naming**: snake_case (mapped via EF Core `HasColumnName`/`ToTable`)
- **Schema migration**: manual SQL in `InventoryService` constructor (`EnsureXxxTable` methods)
- EF Core connection string is cached for 5 minutes in `InventoryDbContext`

## Configuration Files (Network Share)
All shared config lives on the network share:
```
\\Win-u5iq2hisnh3\e\JandH Inventory\Installation\Application Files\Configs\
  types.txt, themes.txt, locations.txt, ColorOrder.txt
  subtypes.json, tracked_types.json, project_tracked_items.json, purchased_from.txt
```
See `ConfigPathService.cs` for paths. Falls back gracefully if share is unavailable.

## Logging
- Primary: `\\Win-u5iq2hisnh3\e\JandH Inventory\Installation\Error Logs\`
- Fallback: `<AppBaseDir>\Logs\`
- One file per day per level: `INFO_yyyy-MM-dd.log`, `ERROR_yyyy-MM-dd.log`, etc.

## User Settings
Stored per-user at: `%LOCALAPPDATA%\MyCraftyStash\settings_{username}.json`
Managed by `UserSettingsService` — supports card size, text size, dark mode, sort orders.

## Build & Run
```bash
cd MyCraftyStash/MyCraftyStash
dotnet restore
dotnet build
dotnet run
```

Publish (ClickOnce is configured; manual self-contained):
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Application Views / Navigation
`MainViewModel` owns all sub-ViewModels. Navigation via `[RelayCommand]` methods:
- **Inventory** — item card grid, detail, add/edit
- **Stock Tracking** — low-stock monitoring
- **Projects** — project cards with linked items
- **Inspiration** — image gallery
- **Sentiment Search** — text-indexed stamp sentiment search (manual crop + user-typed text)
- **Social** — social sharing view
- **Wishlist** — want-list with TE import
- **Purchase Report** — spending reports / export
- **Envelope Expert** — WebView2 embedded tool
- **Address Book** — mailing address management
- **Calendar** — events with reminder dialogs on startup

## Coding Conventions
- Use `[ObservableProperty]` and `[RelayCommand]` from CommunityToolkit.Mvvm
- ViewModels extend `BaseViewModel` (which extends `ObservableObject`)
- Database access: always `using var context = new InventoryDbContext()` (no DI container)
- All DB errors logged via `LoggingService.LogDatabaseError()`
- Table/column names are snake_case; C# properties are PascalCase
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
