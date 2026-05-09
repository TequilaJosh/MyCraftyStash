# My Crafty Stash

A standalone Windows desktop application for managing your craft supplies collection — stamps, dies, stencils, embellishments, and card builds.

## Features

- **Inventory Management**: Add, view, edit, and delete craft items (stamps, dies, stencils, etc.)
- **Photo Storage**: Store images for each item
- **Search & Filter**: Search by name, theme, or sentiments; filter by item type
- **Item Details**: Track price, purchase date, and item numbers
- **Related Items**: Link items together (e.g., matching stamps and dies) with bidirectional relationships
- **Projects**: Track projects and which items were used in each

## System Requirements

- Windows 10/11
- .NET 8.0 Runtime (or SDK for development)

## Building the Application

### Prerequisites

1. Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Windows with WPF support

### Build Steps

1. Open a terminal/command prompt
2. Navigate to the project folder:
   ```
   cd MyCraftyStash
   ```
3. Restore packages:
   ```
   dotnet restore
   ```
4. Build the application:
   ```
   dotnet build
   ```
5. Run the application:
   ```
   dotnet run
   ```

### Creating a Standalone Executable

To create a self-contained executable that doesn't require .NET to be installed:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The executable will be in `bin/Release/net8.0-windows/win-x64/publish/`

## Project Structure

```
MyCraftyStash/
├── Models/                 # Data models (Item, Project, etc.)
├── ViewModels/             # MVVM ViewModels
├── Views/                  # XAML UI views
├── Services/               # Business logic and data access
├── Data/                   # Database context
├── Converters/             # Value converters for XAML
├── App.xaml                # Application resources and styles
├── MainWindow.xaml         # Main application window
└── MyCraftyStash.csproj
```

## Database Configuration

Edit **appsettings.json** in the application folder to set your connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "YOUR_CONNECTION_STRING_HERE"
  }
}
```

### Supported Database Providers

The app auto-detects the database type based on your connection string:

- **SQLite**: `Data Source=C:\path\to\inventory.db`
- **PostgreSQL**: `Host=localhost;Database=inventory;Username=user;Password=pass`
- **SQL Server**: `Server=localhost;Database=inventory;Trusted_Connection=True;`

If no valid connection string is provided, the app falls back to a local SQLite database at:
```
%LOCALAPPDATA%\MyCraftyStash\inventory.db
```

## Usage

### Adding Items

1. Click "Add Item" in the sidebar
2. Fill in the item details:
   - Name (required)
   - Type (Stamp, Die, Stencil, Combo, Paper, Embellishment, Other)
   - Theme
   - Sentiments
   - Image URL
   - Price
   - Date Purchased
   - Item Number
3. Optionally select related items from the list
4. Click "Create Item"

### Viewing Item Details

1. Click on any item card in the Inventory view
2. View all item details including related items
3. Click on related items to navigate to them

### Creating Projects

1. Click "Add Project" in the sidebar
2. Enter project name and description
3. Add an image URL
4. Select items used in the project
5. Click "Create Project"

## Data Migration from Web App

If you have existing data in the web application, you can export it and import it into the desktop app's SQLite database using standard SQLite tools.
