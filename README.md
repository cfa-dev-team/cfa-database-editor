# CFA Database Editor

Desktop application for editing the [CFA (Cardfight!! Area)](https://github.com/uniquekid/cfa-texts) card database. Built with Avalonia UI and .NET 8.

**Version 1.1.1 beta** | Developed by Sieg, 2026

## Features

- **Card Editor** - Edit all card properties (name, text, grade, power, shield, nation, clan, triggers, persona ride, token generators, search effects, arms, legality, and 50+ more fields)
- **Card List** - Browse all 15,000+ cards with search (by name, text, or ID) and filtering (by nation, clan, grade, or absence of nation/clan)
- **Custom Clans & Nations** - Full support for CFA's custom factions system. Create and manage custom clans/nations via a visual dialog, add cards to them with smart ID allocation (configurable start ID, max 31999), and edit custom card IDs. Reads and writes `Custom Overrides.txt` automatically
- **Duplicate Name Detection** - Warns when a card name is already used by another card (CFA engine limitation) with a one-click fix that appends trailing spaces
- **Image Management** - View card images, replace/add images with automatic resizing to 300px (CardSprite) and 75px (CardSpriteMini2) JPEG
- **In-Game Text Preview** - Live preview of card text rendered with the CFA client's icon and formatting pipeline
- **EN Database Sync** - Scrape official English card data from en.cf-vanguard.com, match to CFA cards by artwork similarity (perceptual hashing), and update names/images
- **JP Card Archive** - Scrape card images from the JP "Today's Card" archive and bulk-import them as new cards with nation/clan assignment
- **MD5 Checksums** - Automatic regeneration of `.md5sums` files on save (custom card images are excluded)
- **Unicode Support** - Built-in files are read/written as UTF-8. Custom faction files support both UTF-8 and Windows-1251 encoding, controlled by the `global.CustomFactionUTF8` flag in `Custom Overrides.txt`. New custom factions default to UTF-8. A one-click conversion tool (Tools > Convert Custom Factions to Unicode) converts legacy Win-1251 custom files to UTF-8
- **Windows-1251 Charset Validation** - The in-game font only supports Windows-1251 characters, so encoding issue warnings and automatic apostrophe sanitization on save remain in place regardless of file encoding

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- macOS or Windows

## Setup

```bash
# Clone the repository (if not already done)
git clone https://github.com/uniquekid/cfa-texts.git
cd cfa-texts

# Navigate to the editor project
cd CfaDatabaseEditor

# Restore NuGet packages
dotnet restore

# Build
dotnet build

# Run
dotnet run --project CfaDatabaseEditor
```

### Publishing

```bash
# Windows (x64) — produces a single .exe
dotnet publish CfaDatabaseEditor/CfaDatabaseEditor.csproj -c Release -r win-x64

# macOS — produces a .app bundle (auto-detects arm64/x64)
./publish-macos.sh

# macOS — specify architecture explicitly
./publish-macos.sh arm64
./publish-macos.sh x64
```

Output is in `CfaDatabaseEditor/bin/Release/net8.0/<rid>/publish/`.

## Usage

### Loading the Database

Click `File` -> `Open Database...` to open the folder containing CFA database.

### Editing Cards

1. Use the **search box** or **nation/clan filters** in the left panel to find a card
2. Click a card to select it - its properties appear in the center panel and image on the right
3. Edit any field - changes are tracked per-file
4. Click **Save All** (or Ctrl+S) to write all modified files

### Adding New Cards

Use **Card > New Card** in the menu and select the target file. The editor will auto-assign the next CardStat number and update `global.AllCard` in NoUse.txt. Custom faction targets also appear in this menu when custom factions are defined.

### Custom Clans & Nations

Open **Tools > Custom Factions...** to manage custom clans and nations. The editor reads and writes `Custom Overrides.txt` in the `Text/` folder.

- **Adding factions** — When adding the first custom faction, you'll be prompted for a starting card ID (default 25000). New cards are assigned IDs from this value up to 31999.
- **Faction types** — Custom nations use their ID in the `DCards` field; custom clans use their ID in `CardInClan`. Use faction IDs 100+ to avoid conflicts with built-in factions.
- **Editable card IDs** — Custom cards have editable Card IDs in the editor (validated for uniqueness).
- **File names** — Each faction maps to a `.txt` file in the `Text/` folder. Subfolder paths are supported (e.g. `MyFolder/Custom Clan.txt`).

On save, the editor writes faction definitions, `global.MaxCustomFaction`, `global.CustomCardStartId`, `global.AllCard`, and `global.CustomFactionUTF8` to `Custom Overrides.txt`. Other lines in the file are preserved.

### Converting Custom Factions to Unicode

If you have existing custom faction files encoded in Windows-1251, use **Tools > Convert Custom Factions to Unicode** to convert them to UTF-8. This reads each file as Win-1251, normalizes line endings, and re-writes it as UTF-8 (no BOM). The `global.CustomFactionUTF8 = true` flag is set in `Custom Overrides.txt` automatically. New custom factions created from scratch already default to UTF-8.

### EN Database Sync

1. Open **Tools > EN Database Sync**
2. Paste a URL like `https://en.cf-vanguard.com/cardlist/cardsearch/?expansion=248`
3. Click **Scrape** - the tool downloads card images and names from all pages
4. Cards are matched to CFA entries by artwork similarity (perceptual hash)
5. Review matches, edit names if needed (encoding issues are highlighted in red)
6. **Approve** entries individually or in bulk, then click **Apply Approved**
7. **Save All** in the main window to persist changes

### Replacing Card Images

Select a card, then click **Replace Image** in the right panel. The image is automatically resized to both 300px and 75px variants.

## Project Structure

```
CfaDatabaseEditor/
  CfaDatabaseEditor/
    Models/          Card data model, clan/nation registry, custom overrides data
    Services/        GML parser/writer, database service, image matching,
                     web scraper, text preprocessor, card text renderer
    ViewModels/      MVVM view models for main window, EN sync, JP archive
    Views/           Avalonia XAML views (main window, EN sync, JP archive,
                     custom factions dialog)
    Controls/        Custom card text preview control
    Converters/      Value converters for UI bindings
    Helpers/         MD5 checksum generator
    Assets/Icons/    67 icon sprites extracted from the CFA client
```

## Dependencies

| Package | Purpose |
|---------|---------|
| Avalonia 11 | UI framework |
| Avalonia.Controls.DataGrid | DataGrid for EN sync tool |
| CommunityToolkit.Mvvm | MVVM source generators |
| AngleSharp | HTML parsing for web scraper |
| SkiaSharp | Image processing and text rendering |
| System.Text.Encoding.CodePages | Windows-1251 encoding support |

## Known Issues

- **Text preview** is an approximation of the CFA client rendering - actual result similarity might vary
